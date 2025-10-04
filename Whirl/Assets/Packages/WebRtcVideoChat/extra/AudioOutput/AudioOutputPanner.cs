using UnityEngine;
using UnityEngine.UI;

namespace Byn.Unity.Examples
{
    /// <summary>
    /// Adjusts the stereo pan of all AudioSources in this GameObject's children
    /// based on a Slider's value (-1 to 1).
    /// </summary>
    public class AudioOutputPanner : MonoBehaviour
    {
        public Slider panSlider;

        private void Update()
        {
            float panValue = panSlider.value;

            AudioSource[] childAudioSources = GetComponentsInChildren<AudioSource>();

            foreach (AudioSource source in childAudioSources)
            {
                source.panStereo = panValue;
            }
        }
    }
}