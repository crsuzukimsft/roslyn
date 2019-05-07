﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.Cascade.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.DocumentHighlighting;
using Microsoft.CodeAnalysis.Host.Mef;

namespace Microsoft.VisualStudio.LanguageServices.LiveShare.Client
{
    [ExportLanguageService(typeof(IDocumentHighlightsService), StringConstants.CSharpLspLanguageName), Shared]
    internal class CSharpLspDocumentHighlightsService : RoslynDocumentHighlightsService
    {
        [ImportingConstructor]
        public CSharpLspDocumentHighlightsService(RoslynLSPClientServiceFactory roslynLSPClientServiceFactory)
            : base(roslynLSPClientServiceFactory)
        {
        }
    }

    [ExportLanguageService(typeof(IDocumentHighlightsService), StringConstants.VBLspLanguageName), Shared]
    internal class VBLspDocumentHighlightsService : RoslynDocumentHighlightsService
    {
        [ImportingConstructor]
        public VBLspDocumentHighlightsService(RoslynLSPClientServiceFactory roslynLSPClientServiceFactory)
            : base(roslynLSPClientServiceFactory)
        {
        }
    }
#if !VS_16_0
    [ExportLanguageService(typeof(IDocumentHighlightsService), StringConstants.TypeScriptLanguageName, WorkspaceKind.AnyCodeRoslynWorkspace), Shared]
    internal class TypeScriptLspDocumentHighlightsService : RoslynDocumentHighlightsService
    {
        [ImportingConstructor]
        public TypeScriptLspDocumentHighlightsService(RoslynLSPClientServiceFactory roslynLSPClientServiceFactory, IVsConfigurationSettings configurationSettings)
            : base(roslynLSPClientServiceFactory)
        {
        }
    }
#endif
}
