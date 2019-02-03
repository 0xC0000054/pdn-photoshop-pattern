/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop Pattern FileType Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (c) 2012-2019 Nicholas Hayes
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
        public static string StaticName
        {
            get
            {
                return "Photoshop Pattern";
            }
        }

        public PatternFileType() : base(
            StaticName,
            FileTypeFlags.SupportsLoading | FileTypeFlags.SupportsSaving | FileTypeFlags.SupportsLayers | FileTypeFlags.SavesWithProgress,
            new string[] {".pat"})
        {
        }

        protected override Document OnLoad(Stream input)
        {
            return PatternLoad.Load(input);
        }

        public override PropertyCollection OnCreateSavePropertyCollection()
        {
            List<Property> props = new List<Property>
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
