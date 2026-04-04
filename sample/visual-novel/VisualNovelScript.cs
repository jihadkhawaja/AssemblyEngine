using AssemblyEngine.Core;
using AssemblyEngine.Scripting;
using System.Runtime.Versioning;

namespace VisualNovelSample;

[SupportedOSPlatform("windows")]
public sealed class VisualNovelScript : GameScript
{
    private const float RevealSpeed = 42f;
    private const float FastRevealSpeed = 164f;
    private const float SkipRevealSpeed = 260f;
    private const float SkipAdvanceDelay = 0.18f;
    private const float MidLayerSpeed = 8f;
    private const float FrontLayerSpeed = 20f;
    private const float IntroDuration = 1.2f;

    private readonly IReadOnlyList<DialogueEntry> _entries = StoryContent.Entries;
    private readonly string _savePath;

    private VisualNovelAudioScript _audio = null!;
    private VisualNovelScene _scene = null!;
    private float _storyTime;
    private float _revealProgress;
    private float _autoAdvanceTimer;
    private float _introTimer;
    private float _midLayerOffset;
    private float _frontLayerOffset;
    private float _statusTimer;
    private int _dialogueIndex;
    private int _revealedCharacters;
    private bool _skipEnabled;
    private string _saveStatusText = string.Empty;

    public VisualNovelScript(string savePath)
    {
        _savePath = savePath;
    }

    private DialogueEntry CurrentEntry => _entries[_dialogueIndex];

    public override void OnLoad()
    {
        _audio = Engine.Scripts.GetScript<VisualNovelAudioScript>()
            ?? throw new InvalidOperationException("VisualNovelAudioScript must be registered before VisualNovelScript loads.");
        _scene = (VisualNovelScene)Scene;
        RestartStory(showStatus: false);
        ResetSaveStatus();
        ApplyLayerPositions();
        ApplyCharacterState(0f);
        UpdateUi();
    }

    public override void OnUpdate(float deltaTime)
    {
        _storyTime += deltaTime;
        UpdateStatusTimer(deltaTime);

        if (HandleImmediateInput())
        {
            ApplyLayerPositions();
            ApplyCharacterState(0f);
            return;
        }

        UpdateReveal(deltaTime);
        UpdateParallax(deltaTime);
        ApplyCharacterState(deltaTime);
    }

    public override void OnDraw()
    {
        UpdateUi();
    }

    private bool HandleImmediateInput()
    {
        if (IsKeyPressed(KeyCode.Tab))
        {
            _skipEnabled = !_skipEnabled;
            ShowStatus(_skipEnabled ? "Skip enabled." : "Skip disabled.");
            _audio.PlayToggle(_skipEnabled);
        }

        if (IsKeyPressed(KeyCode.F5))
            SaveProgress();

        if (IsKeyPressed(KeyCode.F9))
        {
            LoadProgress();
            return true;
        }

        if (IsKeyPressed(KeyCode.Home))
        {
            RestartStory();
            return true;
        }

        if (IsKeyPressed(KeyCode.Enter) || IsKeyPressed(KeyCode.Space) || IsKeyPressed(KeyCode.Right))
        {
            AdvanceOrReveal();
            return true;
        }

        return false;
    }

    private void UpdateReveal(float deltaTime)
    {
        var lineLength = CurrentEntry.Line.Length;
        if (_revealedCharacters < lineLength)
        {
            var speed = _skipEnabled
                ? SkipRevealSpeed
                : IsKeyDown(KeyCode.Shift) || IsKeyDown(KeyCode.Control)
                    ? FastRevealSpeed
                    : RevealSpeed;

            _revealProgress = MathF.Min(lineLength, _revealProgress + (deltaTime * speed));
            _revealedCharacters = Math.Min(lineLength, (int)_revealProgress);

            if (!_skipEnabled && CurrentEntry.Speaker != SpeakerRole.Narrator)
                _audio.PlayTalk(CurrentEntry.Speaker);

            if (_revealedCharacters >= lineLength)
            {
                _revealedCharacters = lineLength;
                _revealProgress = lineLength;
                _autoAdvanceTimer = SkipAdvanceDelay;
            }

            return;
        }

        if (!_skipEnabled || _dialogueIndex >= _entries.Count - 1)
            return;

        _autoAdvanceTimer = Math.Max(0f, _autoAdvanceTimer - deltaTime);
        if (_autoAdvanceTimer <= 0f)
            AdvanceLine();
    }

    private void UpdateParallax(float deltaTime)
    {
        _midLayerOffset = WrapLayerOffset(_midLayerOffset - (MidLayerSpeed * deltaTime));
        _frontLayerOffset = WrapLayerOffset(_frontLayerOffset - (FrontLayerSpeed * deltaTime));
        ApplyLayerPositions();
    }

    private void ApplyLayerPositions()
    {
        _scene.MidLayerA.Position = new Vector2(_midLayerOffset, 0);
        _scene.MidLayerB.Position = new Vector2(_midLayerOffset + VisualNovelScene.LayerWidth, 0);
        _scene.FrontLayerA.Position = new Vector2(_frontLayerOffset, 0);
        _scene.FrontLayerB.Position = new Vector2(_frontLayerOffset + VisualNovelScene.LayerWidth, 0);
    }

    private void ApplyCharacterState(float deltaTime)
    {
        var introProgress = EaseOutCubic(Math.Clamp(_introTimer / IntroDuration, 0f, 1f));
        if (_introTimer < IntroDuration)
            _introTimer = Math.Min(IntroDuration, _introTimer + deltaTime);

        var entry = CurrentEntry;
        var irisSpeaking = entry.Speaker == SpeakerRole.Iris && !IsLineComplete();
        var rowanSpeaking = entry.Speaker == SpeakerRole.Rowan && !IsLineComplete();

        var irisBase = Vector2.Lerp(new Vector2(-220, VisualNovelScene.IrisAnchor.Y), VisualNovelScene.IrisAnchor, introProgress);
        var rowanBase = Vector2.Lerp(new Vector2(VisualNovelScene.ViewportWidth + 180, VisualNovelScene.RowanAnchor.Y), VisualNovelScene.RowanAnchor, introProgress);

        var irisBob = MathF.Sin((_storyTime * 1.65f) + 0.4f) * 5f;
        var rowanBob = MathF.Sin((_storyTime * 1.45f) + 1.2f) * 4f;

        var irisFocusOffset = entry.FocusSpeaker == SpeakerRole.Iris ? new Vector2(16, -8) : new Vector2(-12, 4);
        var rowanFocusOffset = entry.FocusSpeaker == SpeakerRole.Rowan ? new Vector2(-18, -8) : new Vector2(12, 5);

        if (entry.FocusSpeaker == SpeakerRole.Narrator)
        {
            irisFocusOffset = new Vector2(-4, 0);
            rowanFocusOffset = new Vector2(4, 0);
        }

        if (irisSpeaking)
            irisFocusOffset += new Vector2(0, MathF.Sin(_storyTime * 14f) * 3f);
        if (rowanSpeaking)
            rowanFocusOffset += new Vector2(0, MathF.Sin((_storyTime * 14f) + 0.8f) * 3f);

        _scene.Iris.Position = irisBase + irisFocusOffset + new Vector2(0, irisBob);
        _scene.Rowan.Position = rowanBase + rowanFocusOffset + new Vector2(0, rowanBob);

        _scene.IrisSprite.SpriteId = ResolvePortraitSprite("iris", irisSpeaking, 0.2f);
        _scene.RowanSprite.SpriteId = ResolvePortraitSprite("rowan", rowanSpeaking, 2.1f);
    }

    private int ResolvePortraitSprite(string prefix, bool isSpeaking, float phase)
    {
        if (isSpeaking && ((int)((_storyTime + phase) * 12f) % 2 == 0))
            return _scene.GetSpriteId(prefix + "-talk");

        var blinkPhase = (_storyTime + phase) % 5.1f;
        if (!isSpeaking && blinkPhase > 4.88f)
            return _scene.GetSpriteId(prefix + "-blink");

        return _scene.GetSpriteId(prefix + "-idle");
    }

    private void AdvanceOrReveal()
    {
        if (!IsLineComplete())
        {
            _revealedCharacters = CurrentEntry.Line.Length;
            _revealProgress = _revealedCharacters;
            _autoAdvanceTimer = SkipAdvanceDelay;
            return;
        }

        if (!AdvanceLine())
        {
            ShowStatus("End of chapter. Press Home to restart.", 3f);
            _audio.PlayChapterEnd();
        }
    }

    private bool AdvanceLine()
    {
        if (_dialogueIndex >= _entries.Count - 1)
            return false;

        _dialogueIndex++;
        PrepareCurrentLine(0);
        _audio.PlayAdvance();
        return true;
    }

    private void RestartStory(bool showStatus = true)
    {
        _dialogueIndex = 0;
        _storyTime = 0f;
        _introTimer = 0f;
        _midLayerOffset = 0f;
        _frontLayerOffset = 0f;
        PrepareCurrentLine(0);
        if (showStatus)
        {
            ShowStatus("Chapter restarted.");
            _audio.PlayRestart();
        }
    }

    private void PrepareCurrentLine(int revealedCharacters)
    {
        _revealedCharacters = Math.Clamp(revealedCharacters, 0, CurrentEntry.Line.Length);
        _revealProgress = _revealedCharacters;
        _autoAdvanceTimer = SkipAdvanceDelay;
    }

    private void SaveProgress()
    {
        var state = new VisualNovelSaveState
        {
            DialogueIndex = _dialogueIndex,
            RevealedCharacters = _revealedCharacters,
            SkipEnabled = _skipEnabled,
            StoryTime = _storyTime,
            IntroTimer = _introTimer,
            MidLayerOffset = _midLayerOffset,
            FrontLayerOffset = _frontLayerOffset,
            SavedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        };

        VisualNovelSaveStore.Save(_savePath, state);
        ShowStatus($"Saved line {_dialogueIndex + 1} of {_entries.Count}.");
        _audio.PlaySave();
    }

    private void LoadProgress()
    {
        if (!VisualNovelSaveStore.TryLoad(_savePath, out var state) || state is null)
        {
            ShowStatus("No save file found.");
            return;
        }

        _dialogueIndex = Math.Clamp(state.DialogueIndex, 0, _entries.Count - 1);
        _skipEnabled = state.SkipEnabled;
        _storyTime = Math.Max(0f, state.StoryTime);
        _introTimer = Math.Clamp(state.IntroTimer, 0f, IntroDuration);
        _midLayerOffset = WrapLayerOffset(state.MidLayerOffset);
        _frontLayerOffset = WrapLayerOffset(state.FrontLayerOffset);
        PrepareCurrentLine(state.RevealedCharacters);
        ApplyLayerPositions();
        ApplyCharacterState(0f);
        ShowStatus("Loaded bookmark.");
        _audio.PlayLoad();
    }

    private void UpdateUi()
    {
        if (Engine.UI is null)
            return;

        var entry = CurrentEntry;
        var line = _revealedCharacters == 0 ? string.Empty : entry.Line[.._revealedCharacters];
        var progressSuffix = _dialogueIndex >= _entries.Count - 1 && IsLineComplete()
            ? "  End"
            : string.Empty;

        Engine.UI.UpdateText("chapter-title", StoryContent.Title);
        Engine.UI.UpdateText("chapter-subtitle", StoryContent.Subtitle);
        Engine.UI.UpdateText("scene-note", entry.SceneNote);
        Engine.UI.UpdateText("speaker-name", GetSpeakerLabel(entry.Speaker));
        Engine.UI.UpdateText("dialogue-text", line);
        Engine.UI.UpdateText("progress-text", $"Line {_dialogueIndex + 1} / {_entries.Count}{progressSuffix}");
        Engine.UI.UpdateText("skip-state", _skipEnabled ? "Skip ON" : "Skip OFF");
        Engine.UI.UpdateText("save-state", _saveStatusText);
        Engine.UI.UpdateText("controls-hint", BuildControlsHint());
    }

    private string BuildControlsHint()
    {
        if (_dialogueIndex >= _entries.Count - 1 && IsLineComplete())
            return "Space or Enter holds on the final line. Home restarts. F5 saves. F9 loads.";

        return "Space or Enter advances. Tab toggles skip. Hold Shift for fast reveal. F5 saves. F9 loads.";
    }

    private void UpdateStatusTimer(float deltaTime)
    {
        if (_statusTimer <= 0f)
            return;

        _statusTimer = Math.Max(0f, _statusTimer - deltaTime);
        if (_statusTimer <= 0f)
            ResetSaveStatus();
    }

    private void ShowStatus(string text, float duration = 2.4f)
    {
        _saveStatusText = text;
        _statusTimer = duration;
    }

    private void ResetSaveStatus()
    {
        _saveStatusText = File.Exists(_savePath)
            ? "F5 save  |  F9 load bookmark"
            : "F5 save  |  F9 load";
    }

    private bool IsLineComplete() => _revealedCharacters >= CurrentEntry.Line.Length;

    private static float WrapLayerOffset(float value)
    {
        while (value <= -VisualNovelScene.LayerWidth)
            value += VisualNovelScene.LayerWidth;

        while (value > 0f)
            value -= VisualNovelScene.LayerWidth;

        return value;
    }

    private static string GetSpeakerLabel(SpeakerRole speaker)
    {
        return speaker switch
        {
            SpeakerRole.Iris => "Iris Vale",
            SpeakerRole.Rowan => "Rowan Hart",
            _ => "Narration"
        };
    }

    private static float EaseOutCubic(float value)
    {
        var inv = 1f - value;
        return 1f - (inv * inv * inv);
    }
}