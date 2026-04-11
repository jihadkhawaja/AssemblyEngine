using AssemblyEngine.Core;
using AssemblyEngine.Platform;

namespace AssemblyEngine.Diagnostics;

internal sealed class InjectedInputController
{
    private readonly object _gate = new();
    private readonly Dictionary<KeyCode, bool> _keyStates = [];
    private readonly HashSet<KeyCode> _dirtyKeys = [];
    private readonly Dictionary<MouseButton, bool> _mouseButtonStates = [];
    private readonly HashSet<MouseButton> _dirtyMouseButtons = [];
    private readonly List<ScheduledKeyRelease> _scheduledKeyReleases = [];
    private readonly List<ScheduledMouseRelease> _scheduledMouseReleases = [];

    private int? _mouseX;
    private int? _mouseY;

    public void QueueKey(RuntimeKeyInputCommand command)
    {
        lock (_gate)
        {
            int holdFrames = Math.Max(1, command.HoldFrames);
            RemoveScheduledKeyRelease(command.Key);

            switch (command.Action)
            {
                case RuntimeInputAction.Down:
                    SetKeyState(command.Key, true);
                    break;

                case RuntimeInputAction.Up:
                    SetKeyState(command.Key, false);
                    break;

                case RuntimeInputAction.Tap:
                    SetKeyState(command.Key, true);
                    _scheduledKeyReleases.Add(new ScheduledKeyRelease(command.Key, holdFrames));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(command.Action), command.Action, "Unknown key input action.");
            }
        }
    }

    public void QueueMouseMove(RuntimeMouseMoveCommand command)
    {
        lock (_gate)
        {
            _mouseX = command.X;
            _mouseY = command.Y;
        }
    }

    public void QueueMouseButton(RuntimeMouseButtonInputCommand command)
    {
        lock (_gate)
        {
            if (command.X.HasValue && command.Y.HasValue)
            {
                _mouseX = command.X.Value;
                _mouseY = command.Y.Value;
            }

            int holdFrames = Math.Max(1, command.HoldFrames);
            RemoveScheduledMouseRelease(command.Button);

            switch (command.Action)
            {
                case RuntimeInputAction.Down:
                    SetMouseButtonState(command.Button, true);
                    break;

                case RuntimeInputAction.Up:
                    SetMouseButtonState(command.Button, false);
                    break;

                case RuntimeInputAction.Tap:
                    SetMouseButtonState(command.Button, true);
                    _scheduledMouseReleases.Add(new ScheduledMouseRelease(command.Button, holdFrames));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(command.Action), command.Action, "Unknown mouse input action.");
            }
        }
    }

    public void ApplyPending()
    {
        lock (_gate)
        {
            if (_mouseX.HasValue && _mouseY.HasValue)
            {
                EngineHost.InjectMousePosition(_mouseX.Value, _mouseY.Value);
                _mouseX = null;
                _mouseY = null;
            }

            foreach (var key in _dirtyKeys)
                EngineHost.InjectKeyState(key, _keyStates[key]);

            _dirtyKeys.Clear();

            foreach (var button in _dirtyMouseButtons)
                EngineHost.InjectMouseButtonState(button, _mouseButtonStates[button]);

            _dirtyMouseButtons.Clear();

            AdvanceKeyReleases();
            AdvanceMouseReleases();
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            foreach (var key in _keyStates.Keys.ToArray())
                SetKeyState(key, false);

            foreach (var button in _mouseButtonStates.Keys.ToArray())
                SetMouseButtonState(button, false);

            _scheduledKeyReleases.Clear();
            _scheduledMouseReleases.Clear();
            _mouseX = null;
            _mouseY = null;
        }
    }

    private void AdvanceKeyReleases()
    {
        for (int index = _scheduledKeyReleases.Count - 1; index >= 0; index--)
        {
            var scheduledRelease = _scheduledKeyReleases[index];
            scheduledRelease.FramesRemaining--;
            if (scheduledRelease.FramesRemaining > 0)
            {
                _scheduledKeyReleases[index] = scheduledRelease;
                continue;
            }

            SetKeyState(scheduledRelease.Key, false);
            _scheduledKeyReleases.RemoveAt(index);
        }
    }

    private void AdvanceMouseReleases()
    {
        for (int index = _scheduledMouseReleases.Count - 1; index >= 0; index--)
        {
            var scheduledRelease = _scheduledMouseReleases[index];
            scheduledRelease.FramesRemaining--;
            if (scheduledRelease.FramesRemaining > 0)
            {
                _scheduledMouseReleases[index] = scheduledRelease;
                continue;
            }

            SetMouseButtonState(scheduledRelease.Button, false);
            _scheduledMouseReleases.RemoveAt(index);
        }
    }

    private void SetKeyState(KeyCode key, bool isDown)
    {
        _keyStates[key] = isDown;
        _dirtyKeys.Add(key);
    }

    private void SetMouseButtonState(MouseButton button, bool isDown)
    {
        _mouseButtonStates[button] = isDown;
        _dirtyMouseButtons.Add(button);
    }

    private void RemoveScheduledKeyRelease(KeyCode key)
    {
        _scheduledKeyReleases.RemoveAll(scheduledRelease => scheduledRelease.Key == key);
    }

    private void RemoveScheduledMouseRelease(MouseButton button)
    {
        _scheduledMouseReleases.RemoveAll(scheduledRelease => scheduledRelease.Button == button);
    }

    private struct ScheduledKeyRelease(KeyCode key, int framesRemaining)
    {
        public KeyCode Key { get; } = key;
        public int FramesRemaining { get; set; } = framesRemaining;
    }

    private struct ScheduledMouseRelease(MouseButton button, int framesRemaining)
    {
        public MouseButton Button { get; } = button;
        public int FramesRemaining { get; set; } = framesRemaining;
    }
}