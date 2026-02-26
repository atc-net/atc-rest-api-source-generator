namespace Atc.CodeGeneration.TypeScript.CodeDocumentation;

public interface IJsDocCommentGenerator
{
    bool ShouldGenerateTags(JsDocComment jsDocComment);

    string GenerateTags(
        ushort indentSpaces,
        JsDocComment jsDocComment);
}
