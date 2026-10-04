using System.Linq;
using UnityEngine;

namespace HeyHeyThere.SoftUISFXFree
{
    /// <summary>
    /// The demo scene: a button per sound, a tab per folder; a click plays it. Drawn with IMGUI, so
    /// it needs no input package.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SoundBoard : MonoBehaviour
    {
        [Tooltip("Each sound's folder.")]
        public string[] groups;
        public AudioClip[] clips;

        string[] tabs;
        int[][] members;  // the clips in each tab
        string[][] names;
        int tab, next;
        string playing = "";
        AudioSource source;

        void Start()
        {
            tabs = groups.Distinct().ToArray();
            members = tabs.Select(t => Enumerable.Range(0, clips.Length).Where(i => groups[i] == t).ToArray()).ToArray();
            names = members.Select(m => m.Select(i => clips[i].name).ToArray()).ToArray();
            source = GetComponent<AudioSource>();
        }

        void OnGUI()
        {
            DemoStyle.Init();
            if (Event.current.type == EventType.Layout)  // a new tab's buttons from the next layout pass on
                tab = next;
            float width = Screen.width - 32;
            GUILayout.BeginArea(new Rect(16, 12, width, Screen.height - 24));
            DemoStyle.Flow(width, tabs, i =>
            {
                if (GUILayout.Toggle(i == next, tabs[i], DemoStyle.Button))
                    next = i;
            });
            GUILayout.Label(playing == "" ? "Click a sound to play it." : $"Playing {playing}", DemoStyle.Hint);
            GUILayout.Space(12);
            var shown = members[tab];
            DemoStyle.Flow(width, names[tab], k =>
            {
                var clip = clips[shown[k]];
                if (!GUILayout.Button(clip.name, DemoStyle.Button))
                    return;
                source.PlayOneShot(clip);
                playing = $"{tabs[tab]}/{clip.name} ({clip.length:0.00} s)";
            });
            GUILayout.EndArea();
        }
    }
}
