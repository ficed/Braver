using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Battle {
    public class Timer {
        private int _increment, _max, _value, _ticks;
        private bool _autoReset;

        private class Event {
            public int When;
            public Action Callback;
            public bool Persistant;
        }

        private List<Event> _events = new();

        public bool IsFull => _value >= _max;
        public int Ticks => _ticks;

        public Timer(int increment, int max, int value, bool autoReset = true) {
            _increment = increment;
            _max = max;
            _value = value;
            _autoReset = autoReset;
        }

        public void Set(int value) {
            _value = value;
        }

        public void On(int value, Action callback, bool persistant = false) {
            _events.Add(new Event {
                When = value,
                Persistant = persistant,
                Callback = callback,
            });
        }
        public void In(int value, Action callback, bool persistant = false) {
            _events.Add(new Event {
                When = value + _value,
                Persistant = persistant,
                Callback = callback,
            });
        }

        public void Reset() {
            _value = 0;
        }

        public void Tick() {
            if (_value < _max) {
                _value += _increment;
                if (_value >= _max) {
                    _ticks++;

                    if (_autoReset) {
                        while (_value >= _max)
                            _value -= _max;
                    }

                    var triggered = _events
                        .Where(e => e.When <= _ticks)
                        .ToArray();
                    _events.RemoveAll(e => (e.When <= _ticks) && !e.Persistant);
                    foreach (var evt in triggered)
                        evt.Callback();
                }
            }
        }
    }
}
