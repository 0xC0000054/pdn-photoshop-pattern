// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.
//
// To add a suppression to this file, right-click the message in the 
// Code Analysis results, point to "Suppress Message", and click 
// "In Suppression File".
// You do not need to add suppressions to this file manually.

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA2210:AssembliesShouldHaveValidStrongNames")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace", Target = "PatternFileTypePlugin")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "2", Scope = "member", Target = "PatternFileTypePlugin.PatternFileType.#OnSaveT(PaintDotNet.Document,System.IO.Stream,PaintDotNet.PropertyBasedSaveConfigToken,PaintDotNet.Surface,PaintDotNet.ProgressEventHandler)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "depth", Scope = "member", Target = "PatternFileTypePlugin.PatternLoad.#LoadPatterns(PatternFileTypePlugin.BinaryReverseReader,System.UInt32)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "enabled", Scope = "member", Target = "PatternFileTypePlugin.PatternLoad.#ReadChannel(PatternFileTypePlugin.BinaryReverseReader,System.Byte[],System.Int32,System.Int32)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "tag", Scope = "member", Target = "PatternFileTypePlugin.PatternLoad.#LoadPatterns(PatternFileTypePlugin.BinaryReverseReader,System.UInt32)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0017:Simplify object initialization", Justification = "Cannot reference properties in an initializer", Scope = "member", Target = "~M:PatternFileTypePlugin.BinaryReverseReader.ReadInt32Rectangle~System.Drawing.Rectangle")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0017:Simplify object initialization", Justification = "Causes Code Analysis warning: CA2000 Dispose objects before losing scope", Scope = "member", Target = "~M:PatternFileTypePlugin.PatternLoad.Load(System.IO.Stream)~PaintDotNet.Document")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "colorMode", Scope = "member", Target = "PatternFileTypePlugin.PatternLoad.#LoadPatterns(PatternFileTypePlugin.BinaryReverseReader,System.UInt32)")]

