using UnityEngine;
using WebRtcCSharp;

namespace Byn.Unity.Examples
{

    /// <summary>
    /// This class is used by UnityAudioOutput to playback raw audio data via
    /// a Unity AudioSource. 
    /// 
    /// </summary>
    public class RemoteAudioPlayer : MonoBehaviour
    {
        /// <summary>
        /// Instance of AudioOutput that manages our virtual output device & tracks. 
        /// Used to receive the raw audio data.
        /// </summary>
        private WebRtcCSharp.AudioOutput mOutput;

        /// <summary>
        /// ID that points towards our virtual output device.
        /// </summary>
        private int mOutputId;

        /// <summary>
        /// Combination of ID's that point towards one specific income audio track.
        /// </summary>
        private TrackIdentity mTrackIdentity;

        /// <summary>
        /// Flag keeps track if the playback is active. When inactive
        /// we stop accessing the C++ layer to reduce performance overhead.
        /// </summary>
        private bool running = false;

        private bool isBuffering = true;
        /// <summary>
        /// If set to true the C++ side has no data available for playback yet and we must
        /// wait for a later time.
        /// </summary>
        public bool IsBuffering
        {
            get
            {
                return isBuffering;
            }
        }

        /// <summary>
        /// Unity audio source used for playback
        /// </summary>
        private AudioSource audioSource;

        /// <summary>
        /// This shows the delay in ms caused by the C++ buffer.
        /// Used for debugging via the Unity Editor
        /// </summary>
        public int delayMs = -1;

        /// <summary>
        /// Buffer that is reused to copy the raw audio data.
        /// </summary>
        private short[] simple_buffer = new short[0];

        public void Awake()
        {
            audioSource = this.gameObject.AddComponent<AudioSource>();
            audioSource.loop = true;

            audioSource.clip = CreateDummyClip();
        }

        /// <summary>
        /// Create a dummy clip that has 1.0f for all samples and channels. 
        /// This way if unity reduces the volume of a sample e.g. 1.0f to 0.5f we can then
        /// multiple the result with our own raw audio to apply the same effect.
        /// NOTE: This clip must be be overwritten in OnAudioFilterRead otherwise
        /// it will cause audible cracking. 
        /// </summary>
        /// <returns>Dummy audio clip in 48kHz sample rate and 2 channels</returns>
        private AudioClip CreateDummyClip()
        {
            int channels = 2;
            int sample_rate = 48000;
            float[] samples = new float[sample_rate * channels];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = 1.0f;
            }
            AudioClip clip = AudioClip.Create("DummyClip", sample_rate, channels, sample_rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Fills the audio buffer with zeros for silence. 
        /// Used to overwrite our dummy clip if no audio playback is available otherwise
        /// we forward audio samples with the value 1 to Unity which will cause cracking.
        /// </summary>
        /// <param name="data"></param>
        void WriteSilence(float[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = 0;
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (running)
            {
                if (simple_buffer.Length != data.Length)
                {
                    simple_buffer = new short[data.Length];
                }
                int res = mOutput.DequeueAudioSamples(mOutputId, mTrackIdentity.GroupId, mTrackIdentity.PeerId, 48000, 2, simple_buffer, simple_buffer.Length);
                if (res < 0)
                {
                    //not enough audio for playback. output silence
                    isBuffering = true;
                    delayMs = -1;
                    WriteSilence(data);
                    return;

                }
                //if res is 0 or greater it shows the samples that are still in the buffer.
                //For now we use this to keep an eye on the delay we are causing:
                //48kHz with 2 channels -> 96 values correspond to 1ms delay
                delayMs = res / (48 * 2);

                //data to read available
                isBuffering = false;
                WriteToFloat(simple_buffer, data);
            }
            else
            {
                //inactive. output silence
                WriteSilence(data);
            }


        }

        public void SetAudioOutput(AudioOutput audioOutput, int device, TrackIdentity identity)
        {
            mOutput = audioOutput;
            mOutputId = device;
            mTrackIdentity = identity;
            Debug.Log("Output set");
        }

        /// <summary>
        /// Conversion from C++ side int16_t to Unity float 
        /// </summary>
        /// <param name="data">Raw data received from C++</param>
        /// <param name="target">float target buffer</param>
        private void WriteToFloat(short[] data, float[] target)
        {
            for (int i = 0; i < target.Length; i++)
            {
                short sample = data[i];
                target[i] = target[i] * (sample / 32768f);
            }
        }
        /// <summary>
        /// Starts playback
        /// </summary>
        public void StartPlayback()
        {
            isBuffering = true;
            running = true;
            audioSource.Play();
        }

        /// <summary>
        /// Stops playback
        /// </summary>
        public void StopPlayback()
        {
            running = false;
            audioSource.Stop();
        }
    }
}