﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Editor.Host;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.FindUsages;
using Microsoft.CodeAnalysis.Navigation;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;
using TPL = System.Threading.Tasks;

namespace Microsoft.VisualStudio.LanguageServices.LiveShare.Client
{
    internal class RoslynGotoDefinitionService : IGoToDefinitionService
    {
        private readonly IStreamingFindUsagesPresenter streamingPresenter;
        private readonly RoslynLSPClientServiceFactory roslynLSPClientServiceFactory;
        private readonly RemoteLanguageServiceWorkspace remoteWorkspace;

        public RoslynGotoDefinitionService(
            IStreamingFindUsagesPresenter streamingPresenter,
            RoslynLSPClientServiceFactory roslynLSPClientServiceFactory,
            RemoteLanguageServiceWorkspace remoteWorkspace) 
        {
            this.streamingPresenter = streamingPresenter ?? throw new ArgumentNullException(nameof(streamingPresenter));
            this.roslynLSPClientServiceFactory = roslynLSPClientServiceFactory ?? throw new ArgumentNullException(nameof(roslynLSPClientServiceFactory));
            this.remoteWorkspace = remoteWorkspace ?? throw new ArgumentNullException(nameof(remoteWorkspace));
        }

        public async TPL.Task<IEnumerable<INavigableItem>> FindDefinitionsAsync(Document document, int position, CancellationToken cancellationToken)
        {
            var definitionItems = await GetDefinitionItemsAsync(document, position, cancellationToken).ConfigureAwait(false);
            if (definitionItems.IsDefaultOrEmpty)
            {
                return ImmutableArray<INavigableItem>.Empty;
            }

            var navigableItems = ImmutableArray.CreateBuilder<INavigableItem>();
            foreach (DocumentSpan documentSpan in definitionItems.SelectMany(di => di.SourceSpans))
            {
                var declaredSymbolInfo = new DeclaredSymbolInfo(Roslyn.Utilities.StringTable.GetInstance(),
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                DeclaredSymbolInfoKind.Class,
                                                Accessibility.NotApplicable,
                                                documentSpan.SourceSpan,
                                                ImmutableArray<string>.Empty);

                navigableItems.Add(NavigableItemFactory.GetItemFromDeclaredSymbolInfo(declaredSymbolInfo, documentSpan.Document));
            }

            return navigableItems.ToArray();
        }

        public bool TryGoToDefinition(Document document, int position, CancellationToken cancellationToken)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                var definitionItems = await GetDefinitionItemsAsync(document, position, cancellationToken).ConfigureAwait(true);
                return await this.streamingPresenter.TryNavigateToOrPresentItemsAsync(document.Project.Solution.Workspace,
                                                                                      "GoTo Definition",
                                                                                      definitionItems).ConfigureAwait(true);
            });
        }

        private async TPL.Task<ImmutableArray<DefinitionItem>> GetDefinitionItemsAsync(Document document, int position, CancellationToken cancellationToken)
        {
            var lspClient = this.roslynLSPClientServiceFactory.ActiveLanguageServerClient;
            if (lspClient == null)
            {
                return ImmutableArray<DefinitionItem>.Empty;
            }

            var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var textDocumentPositionParams = document.GetTextDocumentPositionParams(text, position);

            var response = await lspClient.RequestAsync(Methods.TextDocumentDefinition, textDocumentPositionParams, cancellationToken).ConfigureAwait(false);
            var locations = ((JToken)response)?.ToObject<LSP.Location[]>();
            if (locations == null)
            {
                return ImmutableArray<DefinitionItem>.Empty;
            }

            var definitionItems = ImmutableArray.CreateBuilder<DefinitionItem>();
            foreach (LSP.Location location in locations)
            {
                DocumentSpan? documentSpan;
                if (lspClient.ProtocolConverter.IsExternalDocument(location.Uri))
                {
                    var externalDocument = this.remoteWorkspace.GetOrAddExternalDocument(location.Uri.LocalPath, document.Project.Language);
                    var externalText = await externalDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
                    var textSpan = location.Range.ToTextSpan(externalText);
                    documentSpan = new DocumentSpan(externalDocument, textSpan);
                }
                else
                {
                    documentSpan = await location.ToDocumentSpanAsync(this.remoteWorkspace, cancellationToken).ConfigureAwait(false);
                    if (documentSpan == null)
                    {
                        continue;
                    }
                }

                definitionItems.Add(DefinitionItem.Create(ImmutableArray<string>.Empty, ImmutableArray<TaggedText>.Empty, documentSpan.Value));
            }

            return definitionItems.ToImmutable();
        }
    }
}
