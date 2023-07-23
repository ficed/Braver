// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver {
    public enum Sfx {
        Cursor = 0,
        SaveReady = 1,
        Invalid = 2,
        Cancel = 3,
        CastMagic = 12,
        EnemyDeath = 21,
        BattleSwirl = 42,
        BuyItem = 261,
        DeEquip = 446,
    }

    public interface IAudioItem : IDisposable {
        void Play(float volume, float pan, bool loop, float pitch);
        void Pause();
        void Resume();
        void Stop();
        bool IsPlaying { get; }
    }

    public interface IAudio {
        void Update();
        void SetMusicVolume(byte volume);
        void SetMusicVolume(byte? volumeFrom, byte volumeTo, float duration);
        void PlayMusic(string name, bool pushContext = false);
        void StopMusic(bool popContext = false);
        void Precache(Sfx which, bool pin);
        void ChannelProperty(int channel, float? pan, float? volume);
        void GetChannelProperty(int channel, out float? pan, out float? volume);
        void StopChannel(int channel);
        void StopLoopingSfx(bool includeChannels);
        void PlaySfx(int which, float volume, float pan, int? channel = null);
        void PlaySfx(Sfx which, float volume, float pan, int? channel = null);

        IAudioItem LoadStream(string path, string file);
        void DecodeStream(Stream s, out byte[] rawData, out int channels, out int frequency);
    }


}
