using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Service for rendering markdown content to HTML with support for syntax highlighting.
/// </summary>
public class MarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseEmojiAndSmiley()
            .UseAutoLinks()
            .Build();
    }

    /// <summary>
    /// Converts markdown text to HTML.
    /// </summary>
    /// <param name="markdown">The markdown text to convert.</param>
    /// <returns>The HTML representation of the markdown.</returns>
    public string ToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        return Markdown.ToHtml(markdown, _pipeline);
    }

    /// <summary>
    /// Converts markdown to a Blazor RenderFragment with proper HTML rendering.
    /// </summary>
    /// <param name="markdown">The markdown text to convert.</param>
    /// <returns>A RenderFragment that can be rendered in a Blazor component.</returns>
    public RenderFragment ToRenderFragment(string markdown)
    {
        return builder =>
        {
            if (string.IsNullOrEmpty(markdown))
            {
                builder.AddContent(0, string.Empty);
                return;
            }

            var html = ToHtml(markdown);
            builder.AddMarkupContent(0, html);
        };
    }

    /// <summary>
    /// Extracts plain text from markdown, removing all formatting.
    /// </summary>
    /// <param name="markdown">The markdown text to extract from.</param>
    /// <returns>Plain text without markdown formatting.</returns>
    public string ExtractPlainText(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        var document = Markdown.Parse(markdown, _pipeline);
        var plainText = new System.Text.StringBuilder();

        foreach (var block in document)
        {
            ExtractTextFromBlock(block, plainText);
        }

        return plainText.ToString().Trim();
    }

    private void ExtractTextFromBlock(Block block, System.Text.StringBuilder plainText)
    {
        if (block is LeafBlock leafBlock && leafBlock.Inline != null)
        {
            ExtractTextFromInline(leafBlock.Inline, plainText);
            plainText.AppendLine();
        }
        else if (block is ContainerBlock containerBlock)
        {
            foreach (var child in containerBlock)
            {
                ExtractTextFromBlock(child, plainText);
            }
        }
    }

    private void ExtractTextFromInline(Inline inline, System.Text.StringBuilder plainText)
    {
        switch (inline)
        {
            case LiteralInline literal:
                plainText.Append(literal.Content);
                break;
            case LineBreakInline:
                plainText.AppendLine();
                break;
            case ContainerInline container:
                var child = container.FirstChild;
                while (child != null)
                {
                    ExtractTextFromInline(child, plainText);
                    child = child.NextSibling;
                }
                break;
        }

        if (inline.NextSibling != null)
        {
            ExtractTextFromInline(inline.NextSibling, plainText);
        }
    }
}