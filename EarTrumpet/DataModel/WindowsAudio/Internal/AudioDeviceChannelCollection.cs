using EarTrumpet.Interop.MMDeviceAPI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace EarTrumpet.DataModel.WindowsAudio.Internal
{
    class AudioDeviceChannelCollection
    {
        public List<AudioDeviceChannel> Channels { get; }

        private readonly int _afChannelVolumesOffset;

        public AudioDeviceChannelCollection(IAudioEndpointVolume deviceVolume)
        {
            var ret = new List<AudioDeviceChannel>();
            for (uint i = 0; i < deviceVolume.GetChannelCount(); i++)
            {
                ret.Add(new AudioDeviceChannel(deviceVolume, i));
            }
            Channels = ret;

            AUDIO_VOLUME_NOTIFICATION_DATA dummy;
            _afChannelVolumesOffset = Marshal.OffsetOf<AUDIO_VOLUME_NOTIFICATION_DATA>(nameof(dummy.afChannelVolumes)).ToInt32();
        }

        public float[] CopyChannelVolumes(IntPtr pNotify, AUDIO_VOLUME_NOTIFICATION_DATA data)
        {
            // afChannelVolumes is a flexible inline array, not a pointer. The notification
            // buffer is only valid for the duration of IAudioEndpointVolumeCallback.OnNotify.
            var count = (int)data.nChannels;
            if (count <= 0 || Channels == null || Channels.Count == 0)
            {
                return null;
            }

            if (count > Channels.Count)
            {
                count = Channels.Count;
            }

            var channelVolumesValues = new float[count];
            Marshal.Copy(IntPtr.Add(pNotify, _afChannelVolumesOffset), channelVolumesValues, 0, count);
            return channelVolumesValues;
        }

        public void ApplyChannelVolumes(float[] channelVolumesValues)
        {
            if (channelVolumesValues == null || Channels == null)
            {
                return;
            }

            var count = Math.Min(channelVolumesValues.Length, Channels.Count);
            for (var i = 0; i < count; i++)
            {
                Channels[i].OnNotify(channelVolumesValues[i]);
            }
        }
    }
}
