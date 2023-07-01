/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop Pattern FileType Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (c) 2012-2020, 2023 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using System.Collections.Generic;
using System.IO;

namespace PatternFileTypePlugin
{
    [PluginSupportInfo(typeof(PluginSupportInfo))]
    public sealed class PatternFileType : PropertyBasedFileType
    {
        public static string StaticName => "Photoshop Pattern";
        private static readonly IReadOnlyList<string> FileExtensions = new string[] { ".pat" };

        public PatternFileType() : base(
            StaticName,
            new FileTypeOptions
            {
                LoadExtensions = FileExtensions,
                SaveExtensions = FileExtensions,
                SupportsLayers = true
            })
        {
        }

        protected override Document OnLoad(Stream input)
        {
            return PatternLoad.Load(input);
        }

        public override PropertyCollection OnCreateSavePropertyCollection()
        {
            List<Property> props = new()
            {
                new BooleanProperty(PropertyNames.RLE, true),
            };

            return new PropertyCollection(props);
        }

        public override ControlInfo OnCreateSaveConfigUI(PropertyCollection props)
        {
            ControlInfo info = CreateDefaultSaveConfigUI(props);
            info.SetPropertyControlValue(PropertyNames.RLE, ControlInfoPropertyNames.DisplayName, string.Empty);
            info.SetPropertyControlValue(PropertyNames.RLE, ControlInfoPropertyNames.Description, Properties.Resources.RLECompression);

            return info;
        }

        protected override void OnSaveT(Document input, Stream output, PropertyBasedSaveConfigToken token, Surface scratchSurface, ProgressEventHandler progressCallback)
        {
            PatternSave.Save(input, output, token, progressCallback);
        }
    }
}
