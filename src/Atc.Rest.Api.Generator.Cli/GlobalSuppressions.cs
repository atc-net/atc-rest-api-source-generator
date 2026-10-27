// CA1515 is reported without a source location on the nested type the compiler emits for a C# 14
// `extension(...)` block, so the .editorconfig severity cannot reach it; the containing classes are internal.
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "OK - CLang14 - extension")]