// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Net;
using Ficedula.FF7.Battle;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Battle {

    public class CameraController : ICameraView {
        private CameraData _data;
        private EmbeddedCameraData _introData;
        private FGame _game;

        private CameraFocusScript _focus;
        private CameraPositionScript _position;
        private Action _onComplete;
        private int _focusWait, _positionWait;
        private Model _source;
        private List<Model> _targets;
        private int _idleCamera;
        private List<BattleCamera> _cameras;
        private PerspView3D _view;

        private Func<Vector3> _getPosition, _getFocus;

        public PerspView3D View => _view;

        public CameraController(FGame g, int camdatNumber, IEnumerable<BattleCamera> cameras) {
            _data = new CameraData(g.Open("battle", $"camdat{camdatNumber}.bin"));
            _introData = new EmbeddedCameraData(g.Open("exe", "IntroCamera.bin"), 0x900000, 0x10D0, 0x1270);
            _game = g;
            _cameras = cameras.ToList();
            ResetToIdleCamera();
        }

        public void ResetToIdleCamera() {
            var cam = _cameras[_idleCamera];
            _view = cam.ToView3D();
            _game.Net.Send(new SetBattleCameraMessage { Camera = cam });
        }

        public void ExecuteIntro(int script, Model source, IEnumerable<Model> targets, Action onComplete) {
            _onComplete = onComplete;
            Trace.WriteLine($"Executing intro camera script {script}");
            _focus = _introData.ReadFocus(script);
            _position = _introData.ReadPosition(script);
            _source = source;
            _targets = targets.ToList();
        }

        public void Execute(int script, Model source, IEnumerable<Model> targets, Action onComplete) {
            _onComplete = onComplete;
            int variation = _game.NextRandom(3);
            Trace.WriteLine($"Executing camera script {script} variation {variation}");
            _focus = _data.ReadFocus(script, variation, false);
            _position = _data.ReadPosition(script, variation, false);
            _source = source;
            _targets = targets.ToList();
        }

        public void ExecuteVictory(Model source, IEnumerable<Model> targets) {
            int variation = _game.NextRandom(3);
            _focus = _data.ReadFocus(0, variation, false);
            _position = _data.ReadPosition(0, variation, false);
            _source = source;
            _targets = targets.ToList();
        }

        private void TriggerCompletionIfNecessary() {
            if ((_position == null) && (_focus == null)) {
                _onComplete?.Invoke();
                _onComplete = null;
            }
        }

        private Func<Vector3> GetTransition(Vector3 start, Vector3 end, int frames) {
            int frame = 0;
            return () => {
                float progress;
                if (frame < frames)
                    progress = 1f * frame / frames;
                else
                    progress = 1f;
                frame++;
                return Vector3.Lerp(start, end, progress);
            };
        }

        private Vector3 AggregatePositions(IEnumerable<Vector3> positions) {
            var positionsArray = positions.ToArray();
            return positionsArray
                    .Aggregate((v1, v2) => v1 + v2) / positionsArray.Length;
        }

        //We want to run our camera transitions at 60fps 
        private const int FRAME_MULTIPLIER = 2;

        private bool Execute(DecodedCameraOp<CameraPositionOpcode> op) {
            switch (op.Opcode) {
                case CameraPositionOpcode.JumpToAttackerJoint:
                    int bone = op.Operands[0];
                    Vector3 offset = new Vector3(op.Operands[1], op.Operands[2], op.Operands[3]);
                    _getPosition = () => _source.GetBonePosition(bone) + offset;
                    return true;
                case CameraPositionOpcode.TransitionToAttackerJoint:
                    bone = op.Operands[0];
                    offset = new Vector3(op.Operands[1], op.Operands[2], op.Operands[3]);
                    var start = _view.CameraPosition;
                    _getPosition = GetTransition(start, _source.GetBonePosition(bone) + offset, op.Operands[4] * FRAME_MULTIPLIER);
                    return true;
                case CameraPositionOpcode.TransitionToTargetJoint:
                    bone = op.Operands[0];
                    offset = new Vector3(op.Operands[1], op.Operands[2], op.Operands[3]);
                    start = _view.CameraPosition;
                    _getPosition = GetTransition(
                        start, 
                        AggregatePositions(_targets.Select(t => t.GetBonePosition(bone) + offset)),
                        op.Operands[4] * FRAME_MULTIPLIER
                    );
                    return true;
                case CameraPositionOpcode.TransitionToIdle:
                    var idleCam = _cameras[_idleCamera];
                    start = _getFocus?.Invoke() ?? Vector3.Zero;
                    _getPosition = GetTransition(
                        start,
                        new Vector3(idleCam.X, idleCam.Y, idleCam.Z),
                        op.Operands[0]
                    );
                    return true;
                case CameraPositionOpcode.LoadPoint:
                    Vector3 fixedPosition = new Vector3(op.Operands[0], op.Operands[1], op.Operands[2]);
                    _getPosition = () => fixedPosition;
                    return true;
                case CameraPositionOpcode.Wait:
                    if (_positionWait > 0) {
                        _positionWait--;
                        _position.Rewind();
                        return false;
                    }
                    return true;
                case CameraPositionOpcode.SetWait:
                    _positionWait = op.Operands[0] * FRAME_MULTIPLIER;
                    return true;
                case CameraPositionOpcode.ConditionalRestart:
                    _position.ConditionalRestart(_positionWait);
                    return true;
                case CameraPositionOpcode.ScriptEnd:
                    _position = null;
                    TriggerCompletionIfNecessary();
                    return false;
                /*
                case CameraPositionOpcode.SetActiveIdleCamera:
                    break;
                case CameraPositionOpcode.LoadIdleCameraPos:
                    break;
                 */
                default:
                    Trace.WriteLine($"Skipping unimplemented CameraPositionOpcode {op.Opcode}");
                    return true;
            }
        }
        private bool Execute(DecodedCameraOp<CameraFocusOpcode> op) {
            switch (op.Opcode) {
                case CameraFocusOpcode.JumpToAttackerJoint:
                    int bone = op.Operands[0];
                    var offset = new Vector3(op.Operands[1], op.Operands[2], op.Operands[3]);
                    var pos = _source.GetBonePosition(bone);
                    Trace.WriteLine($"Focus: Jump to {pos} offset {offset}");
                    _getFocus = () => pos + offset;
                    return true;
                case CameraFocusOpcode.TransitionToAttackerJoint:
                    bone = op.Operands[0];
                    offset = new Vector3(op.Operands[1], op.Operands[2], op.Operands[3]);
                    var start = _getFocus?.Invoke() ?? Vector3.Zero;
                    _getFocus = GetTransition(start, _source.GetBonePosition(bone) + offset, op.Operands[4] * FRAME_MULTIPLIER);
                    return true;
                case CameraFocusOpcode.TransitionToTargetJoint:
                    bone = op.Operands[0];
                    offset = new Vector3(op.Operands[1], op.Operands[2], op.Operands[3]);
                    start = _getFocus?.Invoke() ?? Vector3.Zero;
                    _getFocus = GetTransition(
                        start,
                        AggregatePositions(_targets.Select(t => t.GetBonePosition(bone) + offset)),
                        op.Operands[4] * FRAME_MULTIPLIER
                    );
                    return true;

                case CameraFocusOpcode.TransitionToIdle:
                    var idleCam = _cameras[_idleCamera];
                    start = _getFocus?.Invoke() ?? Vector3.Zero;
                    _getFocus = GetTransition(
                        start,
                        new Vector3(idleCam.LookAtX, idleCam.LookAtY, idleCam.LookAtZ),
                        op.Operands[0]
                    );
                    return true;

                case CameraFocusOpcode.Wait:
                    if (_focusWait > 0) {
                        _focusWait--;
                        _focus.Rewind();
                        return false;
                    }
                    return true;
                case CameraFocusOpcode.SetWait:
                    _focusWait = op.Operands[0] * FRAME_MULTIPLIER;
                    return true;
                case CameraFocusOpcode.LoadPoint:
                    Vector3 fixedFocus = new Vector3(op.Operands[0], op.Operands[1], op.Operands[2]);
                    _getFocus = () => fixedFocus;
                    return true;
                case CameraFocusOpcode.ConditionalRestart:
                    _focus.ConditionalRestart(_focusWait);
                    return true;
                /*
                case CameraFocusOpcode.SetActiveIdleCamera:
                    break;
                case CameraFocusOpcode.LoadIdleCameraPos:
                    break;
                 */
                case CameraFocusOpcode.ScriptEnd:
                    _focus = null;
                    TriggerCompletionIfNecessary();
                    return false;
                default:
                    Trace.WriteLine($"Skipping unimplemented CameraFocusOpcode {op.Opcode}");
                    return true;
            }
        }

        public void Step() {
            if (_focus != null)
                while (Execute(_focus.NextOp())) { }

            if (_position != null)
                while (Execute(_position.NextOp())) { }

            var camera = _view.Clone();
            if (_getPosition != null)
                camera.CameraPosition = _getPosition();
            if (_getFocus != null) {
                var focus = _getFocus();
                camera.CameraForwards = focus - camera.CameraPosition;
            }
            _view = camera;
        }

    }
}
