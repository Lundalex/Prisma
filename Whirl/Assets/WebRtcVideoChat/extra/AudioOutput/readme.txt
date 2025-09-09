====================================
Introduction:
====================================
The AudioOutput example shows how a C# application can get access to the raw PCM audio received via WebRTC 
and play it via a Unity AudioSource. 
Future applications for this might be custom audio mixing, audio relay, audio panning or spatial audio.

Test the application by connecting to another CallApp example that sends audio. Once connected, use the slider to
change stereo panning. 

====================================
Scripts overview:
====================================

AudioOutputCallApp.cs: This class will behave similarly to the default CallApp example but reroute audio to 
a virtual audio device managed by UnityAudioOutput.cs.

UnityAudioOutput.cs: It provides a virtual output audio device that will reroute audio to Unity C#. 
UnityAudioOutput will monitor active audio tracks and create a RemoteAudioPlayer for each new incoming audio track. 

RemoteAudioPlayer.cs: Instances of this class will create an AudioSource and play back audio it receives.

====================================
Pitfalls:
====================================
* AudioOutput is still in development and the API can change without warning
* Only one instance of UnityAudioOutput is supported
* Tested only with 1-to-1 calls
