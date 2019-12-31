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

using System;
using System.Reflection;
using PaintDotNet;

namespace PatternFileTypePlugin
{
    public sealed class PluginSupportInfo : IPluginSupportInfo
    {
        public string Author => "null54";

        public string Copyright => ((AssemblyCopyrightAttribute)typeof(PatternFileType).Assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false)[0]).Copyright;

        public string DisplayName => PatternFileType.StaticName;

        public Version Version => typeof(PatternFileType).Assembly.GetName().Version;

        public Uri WebsiteUri => new Uri("http://forums.getpaint.net/index.php?/topic/25696-photoshop-pattern-filetype/");
    }
}
