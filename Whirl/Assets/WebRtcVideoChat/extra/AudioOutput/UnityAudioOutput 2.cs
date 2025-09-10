using Byn.Awrtc.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Byn.Unity.Examples
{
    /// <summary>
    /// This class creates a virtual output device that can be used
    /// to reroute audio from the C++ layer to C#. 
    /// 
    /// To send audio to this new device set the NativeMediaConfig.AudioOutput to the value of
    /// DEVICE_NAME below. 
    /// 
    /// UnityAudioOutput will monitor active incoming audio tracks and create a RemoteAudioPlayer
    /// for each new track. The RemoteAudioPlayer will then handle the actual playback.
    /// 
    /// </summary>
    public class UnityAudioOutput : MonoBehaviour
    {
        /// <summary>
        /// Name of the virtual device we create.
        /// Set in NativeMediaConfig.AudioOutput to route the audio to the correct output.
        /// </summary>
        public static readonly string DEVICE_NAME = "unity_audio_output";

        /// <summary>
        /// Reference to C++ wrapper that handles virtual audio devices for us
        /// </summary>
        private WebRtcCSharp.AudioOutput mOutput;

        /// <summary>
        /// ID is used to retrieve audio frames for our device.
        /// </summary>
        int mOutputId;

        /// <summary>
        /// Stores incoming audio track ID's and associated players
        /// </summary>
        private Dictionary<TrackIdentity, RemoteAudioPlayer> mTrackPlayers = new Dictionary<TrackIdentity, RemoteAudioPlayer>();

        //Helper to output an error when the user attempts to create more than one.
        private static bool mIsCreated = false;

        public void Awake()
        {
            if (mIsCreated)
            {
                Debug.LogError("More than one instance of UnityAudioOutput created. This will result in errors!");
            }
            mIsCreated = true;
        }

        private void OnDestroy()
        {
            mIsCreated = false;
        }

        void Start()
        {
            int sampleRate = AudioSettings.outputSampleRate;
            if (sampleRate != 48000)
            {
                Debug.LogError("Player is only set up to run with 48kHz sample rate. stopping");
                this.enabled = false;
            }
            else
            {

                UnityCallFactory.EnsureInit(OnCallFactoryReady, OnCallFactoryFailed);
            }
        }

        protected virtual void OnCallFactoryReady()
        {
            if (DEVICE_NAME != null && mOutput == null)
            {
                mOutput = (UnityCallFactory.Instance.InternalFactory as Byn.Awrtc.Native.NativeAwrtcFactory).NativeFactory.GetAudioOutput();
                Debug.Log("Creating audio device named " + DEVICE_NAME);
                mOutputId = mOutput.AddDevice(DEVICE_NAME);
                Debug.Log(DEVICE_NAME + " is using ID " + mOutputId);
            }
        }

        protected virtual void OnCallFactoryFailed(string error)
        {
        }


        public void Update()
        {
            if (this.mOutput != null && this.mOutputId > 0)
            {
                //sync with C++ side thread and 
                mOutput.Update();

                //Check if new tracks were created or destroyed
                var tracks = mOutput.GetTrackIdentity(this.mOutputId);
                var activeIds = new TrackIdentity[tracks.Count];
                for (int i = 0; i < tracks.Count; i++)
                {
                    activeIds[i] = new TrackIdentity(tracks[i].groupId, tracks[i].peerId);
                }

                var idsToAdd = activeIds.Where(id => !mTrackPlayers.ContainsKey(id)).ToList();
                var idsToRemove = mTrackPlayers.Keys.Where(id => !activeIds.Contains(id)).ToList();

                foreach (var id in idsToAdd)
                {
                    Debug.Log("Adding audio player for " + id);
                    CreatePlayer(id);
                }
                foreach (var id in idsToRemove)
                {
                    Debug.Log("Removing audio player for " + id);
                    DestroyPlayer(id);
                }
            }
        }

        /// <summary>
        /// Creates a new RemoteAudioPlayer instance for a new incoming audio track.
        /// </summary>
        /// <param name="id"></param>
        private void CreatePlayer(TrackIdentity id)
        {
            var go = new GameObject("AudioOutput" + id);
            go.transform.parent = this.transform;
            var audioPlayer = go.AddComponent<RemoteAudioPlayer>();
            mTrackPlayers[id] = audioPlayer;
            audioPlayer.SetAudioOutput(mOutput, mOutputId, id);
            audioPlayer.StartPlayback();
        }

        private void DestroyPlayer(TrackIdentity id)
        {
            var player = mTrackPlayers[id];
            player.StopPlayback();
            mTrackPlayers.Remove(id);
            Destroy(player.gameObject);
        }
    }

    /// <summary>
    /// This class contains several ID's that identify a specific 
    /// incoming audio track.
    /// Note this ID's might change in the future!
    /// </summary>
    public readonly struct TrackIdentity : IEquatable<TrackIdentity>
    {
        /// <summary>
        /// Group of peers
        /// </summary>
        public readonly int GroupId { get; }

        /// <summary>
        /// Peer the track is associated with.
        /// </summary>
        public readonly int PeerId { get; }


        public TrackIdentity(int groupId, int peerId)
        {
            GroupId = groupId;
            PeerId = peerId;
        }

        public override bool Equals(object obj) =>
            obj is TrackIdentity other && Equals(other);

        public bool Equals(TrackIdentity other) =>
            GroupId == other.GroupId && PeerId == other.PeerId;

        public override int GetHashCode() =>
            HashCode.Combine(GroupId, PeerId);

        public static bool operator ==(TrackIdentity left, TrackIdentity right) =>
            left.Equals(right);

        public static bool operator !=(TrackIdentity left, TrackIdentity right) =>
            !(left == right);

        /// <summary>
        /// Returns a string that represents the current <see cref="TrackIdentity"/>.
        /// </summary>
        public override string ToString() =>
            $"TrackIdentity [GroupId={GroupId}, PeerId={PeerId}]";
    }
}