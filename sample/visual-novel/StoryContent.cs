namespace VisualNovelSample;

internal enum SpeakerRole
{
    Narrator,
    Iris,
    Rowan
}

internal readonly record struct DialogueEntry(
    SpeakerRole Speaker,
    string Line,
    SpeakerRole FocusSpeaker,
    string SceneNote);

internal static class StoryContent
{
    public const string Title = "Lantern Letters";
    public const string Subtitle = "Chapter 1  Rooftop Promise";

    public static IReadOnlyList<DialogueEntry> Entries { get; } =
    [
        new(
            SpeakerRole.Narrator,
            "Rain beads along the brass railings while the last lantern on the academy roof refuses to go out.",
            SpeakerRole.Iris,
            "The observatory floats above the bay, half hidden by low cloud."),
        new(
            SpeakerRole.Iris,
            "You came. I thought the storm would scare even you away from the roof.",
            SpeakerRole.Iris,
            "Iris waits by the telescope with a folded letter tucked inside her sleeve."),
        new(
            SpeakerRole.Rowan,
            "Not when your note ended with meet me before the stars disappear. That sounded expensive.",
            SpeakerRole.Rowan,
            "Rowan arrives with a satchel of paper charms and too much confidence."),
        new(
            SpeakerRole.Iris,
            "It is. The headmaster is closing the observatory at dawn, and the sky archive goes with it.",
            SpeakerRole.Iris,
            "Silver ink glints across the page in her hand."),
        new(
            SpeakerRole.Rowan,
            "Then we copy what matters tonight. Tell me where you hid the charts.",
            SpeakerRole.Rowan,
            "The lantern swings once as thunder rolls past the hill."),
        new(
            SpeakerRole.Iris,
            "Behind the false shelf in the west wall. I knew you would say yes before I finished asking.",
            SpeakerRole.Iris,
            "For the first time all evening, the tension in her shoulders loosens."),
        new(
            SpeakerRole.Narrator,
            "A train glides under the hill, casting pale bands of light across the glass dome.",
            SpeakerRole.Rowan,
            "For one breath, the roof feels less like a school and more like a station between futures."),
        new(
            SpeakerRole.Rowan,
            "If we are stealing a future, we should at least make it look elegant.",
            SpeakerRole.Rowan,
            "Rowan pulls a charcoal pencil from behind one ear and grins at the storm."),
        new(
            SpeakerRole.Iris,
            "Elegant I can do. Quiet depends on whether the weather keeps covering for us.",
            SpeakerRole.Iris,
            "The old weather vane chatters above them like an impatient metronome."),
        new(
            SpeakerRole.Rowan,
            "Then let the clouds keep watch. I will handle the locks if you handle the legends.",
            SpeakerRole.Rowan,
            "He lifts the lantern so both of them can read without stepping closer to the ledge."),
        new(
            SpeakerRole.Narrator,
            "The observatory shutters begin to open overhead, one iron petal at a time.",
            SpeakerRole.Iris,
            "Some promises are written on paper. The dangerous ones are written in starlight."),
        new(
            SpeakerRole.Iris,
            "Stay with me until sunrise, Rowan. If the observatory ends tonight, let the story begin here.",
            SpeakerRole.Iris,
            "The first hidden chart slides free from the wall behind the telescope."),
    ];
}