using Byn.Awrtc;
using Byn.Awrtc.Native;
using UnityEngine;

namespace Byn.Unity.Examples
{
    /// <summary>
    /// Call app that will deactivate the default audio playback and instead
    /// forward audio to the RawAudioPlayer for custom audio processing.
    /// 
    /// Note mCall.SetVolume(0, args.ConnectionId); must be called for ever connection at the moment.
    /// This blocks the internal system from playing back the audio while C# still receives
    /// the full audio via AudioFramesEventArgs. 
    /// </summary>
    public class AudioOutputCallApp : CallApp
    {
        public UnityAudioOutput _AudioPlayer = null;

        protected override void OnCallFactoryReady()
        {
            base.OnCallFactoryReady();
        }
        public static int GetVolume(AudioFrames frames)
        {
            double res = 0;
            var samples = frames.Samples;
            for (int i = 0; i < samples.Length; i++)
            {
                res += Mathf.Abs(samples[i]);
            }
            res = res / samples.Length;
            return (int)res;
        }
        public override MediaConfig CreateMediaConfig()
        {
            //must use native media config. This ensures we have the correct C# libraries included
            //(and should trigger a compiler error if used on unsupported platforms such as WebGL)
            NativeMediaConfig nativeConfig = base.CreateMediaConfig() as NativeMediaConfig;
            if (nativeConfig != null)
            {
                if (UnityAudioOutput.DEVICE_NAME != null)
                {
                    //audio output via AudioOutput class to bypass update loop
                    //and instead use unity's thread that calls audio filter
                    nativeConfig.AudioAccess = false;
                    nativeConfig.AudioOutput = UnityAudioOutput.DEVICE_NAME;
                }
                //turning off echo cancellation for testing via local loopback connections
                //note it isn't clear how well echo cancellation will work with custom playback
                nativeConfig.AudioOptions.echo_cancellation = false;
            }
            else
            {
                //indicates the base class creates a cross platform MediaConfig instead of the 
                //NativeMediaConfig used for windows.
                Debug.LogError("No NativeMediaConfig used by the base CallApp. Is the wrong platform active? Make sure PC / Standalone is set to active in the Unity build settings!");
            }
            return nativeConfig;
        }

        protected override void Call_CallEvent(object sender, CallEventArgs e)
        {
            base.Call_CallEvent(sender, e);
            if (e.Type == CallEventType.CallAccepted)
            {
                var args = e as CallAcceptedEventArgs;
                //turn off playback via the internal system
                mCall.SetVolume(0, args.ConnectionId);
            }
        }
    }
}