using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Foundation.Collections;
using System.Runtime.InteropServices;
using Windows.Media;
using Windows.Foundation;
using System.Diagnostics;

namespace AudioEffectComponent
{
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    public sealed class ExampleAudioEffect : IBasicAudioEffect
    {
        private readonly AudioEncodingProperties[] _supportedEncodingProperties = new AudioEncodingProperties[]
{
        AudioEncodingProperties.CreatePcm(44100, 1, 32),
        AudioEncodingProperties.CreatePcm(48000, 1, 32),
};

        private AudioEncodingProperties _currentEncodingProperties;
        private IPropertySet _propertySet;

        private readonly Queue<float> _echoBuffer = new Queue<float>(100000);
        private int _delaySamplesCount;

        private float Delay
        {
            get
            {
                if (_propertySet != null && _propertySet.TryGetValue("Delay", out object val))
                {
                    return (float)val;
                }
                return 500.0f;
            }
        }

        private float Feedback
        {
            get
            {
                if (_propertySet != null && _propertySet.TryGetValue("Feedback", out object val))
                {
                    return (float)val;
                }
                return 0.5f;
            }
        }

        private float Mix
        {
            get
            {
                if (_propertySet != null && _propertySet.TryGetValue("Mix", out object val))
                {
                    return (float)val;
                }
                return 0.5f;
            }
        }

        public bool UseInputFrameForOutput { get { return true; } }

        public IReadOnlyList<AudioEncodingProperties> SupportedEncodingProperties { get { return _supportedEncodingProperties; } }

        public void SetProperties(IPropertySet configuration)
        {
            _propertySet = configuration;
        }

        public void SetEncodingProperties(AudioEncodingProperties encodingProperties)
        {
            _currentEncodingProperties = encodingProperties;

            // compute the number of samples for the delay
            _delaySamplesCount = (int)MathF.Round((this.Delay / 1000.0f) * encodingProperties.SampleRate);

            // fill empty samples in the buffer according to the delay
            for (int i = 0; i < _delaySamplesCount; i++)
            {
                _echoBuffer.Enqueue(0.0f);
            }
        }

        unsafe public void ProcessFrame(ProcessAudioFrameContext context)
        {
            AudioFrame frame = context.InputFrame;

            using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.ReadWrite))
            using (IMemoryBufferReference reference = buffer.CreateReference())
            {
                ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* dataInBytes, out uint capacity);
                float* dataInFloat = (float*)dataInBytes;
                int dataInFloatLength = (int)buffer.Length / sizeof(float);

                // read parameters once
                float currentWet = this.Mix;
                float currentDry = 1.0f - currentWet;
                float currentFeedback = this.Feedback;

                // Process audio data
                float sample, echoSample, outSample;
                for (int i = 0; i < dataInFloatLength; i++)
                {
                    // read values
                    sample = dataInFloat[i];
                    echoSample = _echoBuffer.Dequeue();

                    // compute output sample
                    outSample = (currentDry * sample) + (currentWet * echoSample);
                    dataInFloat[i] = outSample;

                    // compute delay sample
                    echoSample = sample + (currentFeedback * echoSample);
                    _echoBuffer.Enqueue(echoSample);
                }
            }
        }

        public void Close(MediaEffectClosedReason reason)
        {
        }

        public void DiscardQueuedFrames()
        {
            // reset the delay buffer
            _echoBuffer.Clear();
            for (int i = 0; i < _delaySamplesCount; i++)
            {
                _echoBuffer.Enqueue(0.0f);
            }
        }
    }
}
