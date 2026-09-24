namespace FlowLang.StandardLibrary.TestFramework;

/// <summary>
/// Phase 35 Plan 35-04 TEST-01 — thrown by the five assertion primitives
/// <c>(assert)</c>, <c>(assertEq)</c>, <c>(assertNotesMatch)</c>,
/// <c>(assertBytesEqual)</c>, <c>(assertWithinDb)</c> when their predicate
/// fails. The <see cref="TestRunner"/> catches this exception to convert a
/// test-body invocation into a FAIL outcome; other code paths let it bubble
/// (matches Flow's existing exception-as-error precedent — see
/// <c>InvalidOperationException</c> use in <c>StdLib.And</c>).
///
/// The single-string-message constructor is the canonical shape per RESEARCH
/// §Example 3. Composers do not catch this from Flow code — there is no
/// try/catch surface in the language; the framework owns the catch site.
/// </summary>
public class AssertionException : Exception
{
    public AssertionException(string message) : base(message) { }

    public AssertionException(string message, Exception innerException)
        : base(message, innerException) { }
}
