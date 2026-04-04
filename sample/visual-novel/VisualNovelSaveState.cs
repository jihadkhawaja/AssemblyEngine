namespace VisualNovelSample;

internal sealed class VisualNovelSaveState
{
    public int DialogueIndex { get; set; }
    public int RevealedCharacters { get; set; }
    public bool SkipEnabled { get; set; }
    public float StoryTime { get; set; }
    public float IntroTimer { get; set; }
    public float MidLayerOffset { get; set; }
    public float FrontLayerOffset { get; set; }
    public string? SavedAtUtc { get; set; }
}