﻿// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Plugins;
using Braver.Plugins.Field;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Braver.Field {
    public class Movie {

        private int _frame;
        private SpriteBatch _spriteBatch;
        private GraphicsDevice _graphics;

        public int Frame => (int)(_frame * _gameFramesPerVideoFrame / 4); //adjust reported frame to reflect 15FPS as the original game expects
        public bool Active => (_process != null) && (_frame >= 0);
        public Ficedula.FF7.Field.CameraMatrix Camera => _cam.Camera.ElementAtOrDefault(Frame);

        private Process _process;
        private Action _onComplete;
        private SoundEffect _soundEffect;
        private SoundEffectInstance _effectInstance;
        private Texture2D _texture;
        private float _gameFramesPerVideoFrame, _frameIncrement;
        private byte[] _framebuffer;

        private static Regex _reSize = new Regex(@"(\d+)x(\d+)");

        private string[] _files;
        private FGame _game;
        private Ficedula.FF7.Field.MovieCam _cam;
        private PluginInstances<IMovie> _plugins;

        private static string[] _extensions = new[] { ".mp4", ".avi" };

        private string ResolvePath(string name) {
            foreach (string ext in _extensions) {
                string fn = Path.Combine(_game.GetPath("Movies"), name + ext);
                if (File.Exists(fn)) return fn;
            }
            return Path.Combine(_game.GetPath("Movies"), name + _extensions[0]); //...although it'll fail at runtime, but lets us continue running for now
        }

        public Movie(FGame g, GraphicsDevice graphics, PluginInstances<IMovie> plugins) {
            _game = g;
            _graphics = graphics;
            _spriteBatch = new SpriteBatch(graphics);
            _plugins = plugins;

            _files = g.OpenString("movies", "movielist.txt")
                .Split('\n')
                .Select(s => s.Trim('\r'))
                .Select(s => ResolvePath(s)) //TODO!!!
                .ToArray();
        }

        private unsafe void GetAudioData(string filename, string customCommand) {

            var psi = new ProcessStartInfo {
                FileName = _game.GetPath("FFMpeg"),
                ArgumentList = {
                        "-i", filename,
                        "-map", "0:a",
                        "-acodec", "pcm_s16le",
                        "-f", "s16le",
                        "-"
                    },
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            if (!string.IsNullOrWhiteSpace(customCommand)) {
                psi.ArgumentList.Clear();
                psi.Arguments = string.Format(customCommand, filename);
            }

            var process = Process.Start(psi);

            bool stereo = false;
            int freq = 0;

            do {
                string s = process.StandardError.ReadLine();
                if (s == null) break;
                if (s.Contains("Audio: pcm_s16le")) {
                    foreach (string part in s.Split(',')) {
                        if (part.EndsWith("Hz"))
                            freq = int.Parse(part.Substring(0, part.Length - 2).Trim());
                        else if (part.Trim().Equals("stereo", StringComparison.InvariantCultureIgnoreCase))
                            stereo = true;
                    }
                }
            } while (freq == 0);

            process.StandardError.Close();

            if (freq > 0) {
                var ms = new System.IO.MemoryStream();
                process.StandardOutput.BaseStream.CopyTo(ms);
                byte[] data = ms.ToArray();

                _soundEffect = new SoundEffect(data, freq, stereo ? AudioChannels.Stereo : AudioChannels.Mono);
                _effectInstance = _soundEffect.CreateInstance();
            }

            process.Dispose();
        }


        public void Prepare(int movie) {

            string filename = _files[movie];

            if (!File.Exists(filename))
                throw new Exception($"Movie file {filename} not found");

            using (var s = _game.Open("cd", Path.GetFileName(Path.ChangeExtension(filename, ".cam"))))
                _cam = new Ficedula.FF7.Field.MovieCam(s);

            GetAudioData(filename, "");

            string ffFormat;
            int bytesPerPixel;
            ffFormat = "rgba";
            bytesPerPixel = 4;

            var psi = new ProcessStartInfo {
                FileName = _game.GetPath("FFMpeg"),
                ArgumentList = {
                        "-hwaccel", "auto",
                        "-i", filename,
                        "-f", "image2pipe",
                        "-pix_fmt", ffFormat,
                        "-vcodec", "rawvideo",
                        "-blocksize", "65536",
                        "-"
                    },
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            var process = Process.Start(psi);

            int stride;
            int width = 0, height = 0;
            float fps = 0;

            do {
                string s = process.StandardError.ReadLine();
                if (s == null) return;
                if (s.Contains("Video: rawvideo")) {
                    foreach (string part in s.Split(',')) {
                        if (part.EndsWith("fps"))
                            fps = float.Parse(part.Substring(0, part.Length - 3).Trim());
                        var m = _reSize.Match(part.Trim());
                        if (m.Success) {
                            width = int.Parse(m.Groups[1].Value);
                            height = int.Parse(m.Groups[2].Value);
                        }
                    }
                }
            } while (width == 0);

            process.StandardError.Close();
            if (fps == 0) fps = 30;

            _gameFramesPerVideoFrame = 60 / fps;
            _frameIncrement = 0;

            stride = width * bytesPerPixel;
            /* //TODO: Necessary or not?
            if ((stride % 32) != 0)
                stride += 32 - (stride % 32);
            */

            _texture = new Texture2D(_graphics, stride / bytesPerPixel, height, false, SurfaceFormat.Color);
            _framebuffer = new byte[_texture.Width * _texture.Height * 4];
            _texture.SetData(_framebuffer);

            //Now we have a texture, make it available to be played
            _process = process;
            _frame = -1;

            _plugins.Call(m => m.Loaded(Path.GetFileName(filename)));
        }

        public void Play(Action onComplete) {
            _frame = 0;
            _effectInstance?.Play();
            _onComplete = onComplete;
            _plugins.Call(m => m.Playing(_frame));
        }

        public void Render() {
            if (_frame >= 0) {

                Rectangle bar0, bar1;

                _spriteBatch.Begin(depthStencilState: DepthStencilState.None);

                float srcRatio = 1f * _texture.Width / _texture.Height,
                    destRatio = 1f * _graphics.Viewport.Width / _graphics.Viewport.Height;

                if (srcRatio <= destRatio) {
                    int widthUsed = (int)(_graphics.Viewport.Height * srcRatio);
                    int xoffset = (_graphics.Viewport.Width - widthUsed) / 2;
                    _spriteBatch.Draw(_texture, new Rectangle(xoffset, 0, widthUsed, _graphics.Viewport.Height), Color.White);
                    bar0 = new Rectangle(0, 0, xoffset, _graphics.Viewport.Height);
                    bar1 = new Rectangle(_graphics.Viewport.Width - xoffset, 0, xoffset, _graphics.Viewport.Height);
                } else {
                    int heightUsed = (int)(_texture.Width / srcRatio);
                    int yoffset = (_graphics.Viewport.Height - heightUsed) / 2;
                    _spriteBatch.Draw(_texture, new Rectangle(0, yoffset, _graphics.Viewport.Width, heightUsed), Color.White);
                    bar0 = new Rectangle(0, 0, _graphics.Viewport.Width, yoffset);
                    bar1 = new Rectangle(0, _graphics.Viewport.Height - yoffset, _graphics.Viewport.Width, yoffset);
                }
                _spriteBatch.End();

                _spriteBatch.Begin(
                    depthStencilState: DepthStencilState.Default,
                    transformMatrix: Matrix.CreateScale(1, 1, 0)
                );
                _spriteBatch.Draw(_texture, bar0, Color.Black);
                _spriteBatch.Draw(_texture, bar1, Color.Black);
                _spriteBatch.End();
            }
        }

        public unsafe void Step() {
            if (_process == null) return;

            _frameIncrement++;

            if (_frameIncrement >= _gameFramesPerVideoFrame) {
                int read = 0, size = _texture.Width * _texture.Height * 4;
                fixed (byte* baseptr = &_framebuffer[0]) {
                    while (read < size) {
                        byte* ptr = baseptr + read;
                        int count = _process.StandardOutput.BaseStream.Read(new Span<byte>(ptr, size - read));

                        if (count <= 0) {
                            _process.Kill();
                            _process.Dispose();
                            _process = null;
                            _onComplete?.Invoke();
                            break;
                        }

                        read += count;
                    }
                }
                _texture.SetData(_framebuffer);
                _frame++;
                _plugins.Call(m => m.Playing(_frame));
                _frameIncrement -= _gameFramesPerVideoFrame;

                if (_process == null)
                    _plugins.Call(m => m.Stopped());
            } else {
                //
            }
        }
    }
}
