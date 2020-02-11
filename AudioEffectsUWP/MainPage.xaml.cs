using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using System.Threading.Tasks;
using Windows.Media.Audio;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Composition;
using AudioEffectComponent;
using Windows.Media.Effects;
using System.Diagnostics;


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AudioEffectsUWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        AudioFileInputNode _fileInputNode;
        AudioDeviceInputNode _deviceInputNode;
        AudioGraph _graph;
        AudioSubmixNode _submixNode;
        AudioDeviceOutputNode _deviceOutputNode;
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void Play_OnClick(object sender, RoutedEventArgs e)
        {
            await CreateGraph();
            await CreateDefaultDeviceOutputNode();
            await CreateFileInputNode();

            // Create submix node
            _submixNode = _graph.CreateSubmixNode();

            AddCustomEcho();
            
            ConnectNodes();

            _graph.Start();
        }

        private void Stop_OnClick(object sender, RoutedEventArgs e)
        {
            _graph.Stop();

        }
        /// <summary>
        /// Create an audio graph that can contain nodes
        /// </summary>       
        private async Task CreateGraph()
        {
            // Specify settings for graph, the AudioRenderCategory helps to optimize audio processing
            AudioGraphSettings settings = new AudioGraphSettings(Windows.Media.Render.AudioRenderCategory.Media);

            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);

            if (result.Status != AudioGraphCreationStatus.Success)
            {
                throw new Exception(result.Status.ToString());
            }

            _graph = result.Graph;
        }

        /// <summary>
        /// Create a node to output audio data to the default audio device (e.g. soundcard)
        /// </summary>
        private async Task CreateDefaultDeviceOutputNode()
        {
            CreateAudioDeviceOutputNodeResult result = await _graph.CreateDeviceOutputNodeAsync();

            if (result.Status != AudioDeviceNodeCreationStatus.Success)
            {
                throw new Exception(result.Status.ToString());
            }

            _deviceOutputNode = result.DeviceOutputNode;
        }

        /// <summary>
        /// Ask user to pick a file and use the chosen file to create an AudioFileInputNode
        /// </summary>
        private async Task CreateFileInputNode()
        {
            FileOpenPicker filePicker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.MusicLibrary,
                FileTypeFilter = { ".mp3", ".wav" }
            };

            StorageFile file = await filePicker.PickSingleFileAsync();

            // file null check code omitted

            CreateAudioFileInputNodeResult result = await _graph.CreateFileInputNodeAsync(file);

            if (result.Status != AudioFileNodeCreationStatus.Success)
            {
                throw new Exception(result.Status.ToString());
            }

            _fileInputNode = result.FileInputNode;

            // Set infinite loop
            _fileInputNode.LoopCount = null;
            // When file finishes playing the first time, 
            // turn it's output gain down to zero.
            _fileInputNode.FileCompleted += _fileInputNode_FileCompleted;            
        }

        private void _fileInputNode_FileCompleted(AudioFileInputNode sender, object args)
        {
                _fileInputNode.OutgoingGain = 0.0;

        }

        /// <summary>
        /// Create an instance of the pre-supplied reverb effect and add it to the output node
        /// </summary>
        private void AddCustomEcho()
        {
            // Built in echo effect
            //EchoEffectDefinition echoEffect = new EchoEffectDefinition(_graph)
            //{
            //    Delay = 2000
            //};

            //_submixNode.EffectDefinitions.Add(echoEffect);

            // Custom effect
            // Create a property set and add a property/value pair
            PropertySet echoProperties = new PropertySet();
            echoProperties.Add("Mix", 0.7f);
            echoProperties.Add("Delay", 500.0f);
            echoProperties.Add("Feedback", 0.5f);

            // Instantiate the custom effect defined in the 'AudioEffectComponent' project
            AudioEffectDefinition echoEffectDefinition = new AudioEffectDefinition(typeof(ExampleAudioEffect).FullName, echoProperties);
            _submixNode.EffectDefinitions.Add(echoEffectDefinition);
        }

        /// <summary>
        /// Connect all the nodes together to form the graph, in this case we only have 2 nodes
        /// </summary>
        private void ConnectNodes()
        {
            _fileInputNode.AddOutgoingConnection(_submixNode);
            _submixNode.AddOutgoingConnection(_deviceOutputNode);
        }
    }
}
