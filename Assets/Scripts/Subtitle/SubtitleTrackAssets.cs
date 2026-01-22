using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// 1. 轨道
[TrackColor(0.2f, 0.8f, 0.2f)]
[TrackBindingType(typeof(FallingTextEmitter))]
[TrackClipType(typeof(SubtitleClip))]
public class SubtitleTrack : TrackAsset { }

// 2. Clip (现在非常干净，什么参数都不用填)
public class SubtitleClip : PlayableAsset
{
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<SubtitleBehaviour>.Create(graph);
    }
}

// 3. 逻辑
public class SubtitleBehaviour : PlayableBehaviour
{
    private bool _hasTriggered = false;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (!Application.isPlaying) return;

        if (!_hasTriggered)
        {
            var emitter = info.output.GetUserData() as FallingTextEmitter;
            if (emitter != null)
            {
                // 【核心】直接把 Clip 的长度传给 Emitter
                // Emitter 会自己算：如果长度是 5秒，我有 5个字，那就 1秒蹦一个字。
                emitter.Speak(playable.GetDuration());
            }
            _hasTriggered = true;
        }
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        _hasTriggered = false;
    }
}