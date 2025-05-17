using CognitiveSupport;

namespace Mutation;

public class SoundFeedbackManager
{
    public void BeepMuted() => BeepPlayer.Play(BeepType.Mute);

    public void BeepUnmuted() => BeepPlayer.Play(BeepType.Unmute);

    public void BeepStart() => BeepPlayer.Play(BeepType.Start);

    public void BeepEnd() => BeepPlayer.Play(BeepType.End);

    public void BeepSuccess() => BeepPlayer.Play(BeepType.Success);

    public void BeepFailure(int repeatCount = 3)
    {
        for (int i = 0; i < repeatCount; i++)
            BeepPlayer.Play(BeepType.Failure);
    }
}
