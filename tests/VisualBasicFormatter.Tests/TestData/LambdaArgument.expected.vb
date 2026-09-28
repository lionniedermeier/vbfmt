Public Class LambdaArguments
    Private ReadOnly _employees As New List(Of Employee)

    ' A block's body cannot be laid out behind the bracket that holds it: wherever the bracket
    ' happens to end, the body would start one level further in, leaving the StartsWith calls no
    ' room and forcing them to break too. It goes below the bracket instead, and the AndAlso/OrElse
    ' runs get their width back.
    Public Sub FiltersByTagPrefix()
        Dim matches = _employees.Where(
            Function(employee) employee.Status = EmployeeStatus.Active AndAlso
                employee.Department IsNot Nothing AndAlso
                employee.Name IsNot Nothing AndAlso
                employee.Tags.Any(
                    Function(tag)
                        Return tag.Length > 3 AndAlso tag.StartsWith("F", StringComparison.OrdinalIgnoreCase) OrElse
                            tag.StartsWith("C", StringComparison.OrdinalIgnoreCase) OrElse
                            tag.StartsWith("A", StringComparison.OrdinalIgnoreCase)
                    End Function
                )
        )
    End Sub

    ' A block last among several ordinary arguments still goes below the bracket, on its own.
    Public Sub RegistersWithCallback()
        Register(
            "primary",
            3,
            Function(name)
                Return Handlers.Contains(name)
            End Function
        )
    End Sub

    ' A block first among several arguments -- the case that pins the trigger to "any element", not
    ' just the last one. With the last element only, this would still align the lambda behind the
    ' opening paren under WrapStyle.Align and starve its body.
    Public Sub RegistersCallbackFirst()
        Register(
            Function(name)
                Return Handlers.Contains(name)
            End Function,
            "primary",
            3
        )
    End Sub

    ' A collection initializer's brace gets the same treatment as an argument list's paren.
    Public Sub BuildsHandlerList()
        Dim handlers = {
            Function()
                Return Work()
            End Function
        }
    End Sub

    ' A multi-line Sub lambda goes below the bracket exactly like a Function lambda. It used to be
    ' excluded, because moving its header onto its own line once tripped a Roslyn equivalence quirk;
    ' that no longer reproduces -- see the remarks on TrailingExpansion.IsBlock.
    Public Sub RegistersCallbackWithoutResult()
        Register(
            Sub()
                Handlers.Add("primary")
            End Sub
        )
    End Sub
End Class
