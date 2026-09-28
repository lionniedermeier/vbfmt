Public Class EmployeeReport
    Private ReadOnly _employees As New List(Of String)

    Public Sub Report()
        ' Breaking at a comma outranks breaking inside the expression a comma delimits: the
        ' comparisons stay whole. The outer If breaks first -- its commas give way before the
        ' trailing nested If is opened up -- and the nested If then gets to stay on its own line
        ' because it fits there on its own.
        Dim nestedConditional = If(
            _employees.Count > 10,
            "Large",
            If(_employees.Count > 5, "Medium", If(_employees.Count > 0, "Small", "Empty"))
        )

        ' A longer head still breaks the same way: outer first. Here the nested If no longer fits
        ' next to the outer's own arguments either, so it breaks in turn -- but only because it
        ' does not fit, not because it was singled out for being last.
        Dim anExtremelyLongNestedConditionalResultNameForDemonstrationPurposes = If(
            _employees.Count > 10,
            "LargeSized",
            If(_employees.Count > 5, "Medium", If(_employees.Count > 0, "Small", "Empty"))
        )

        ' The two-argument form.
        Dim fallback = If(
            _employees.FirstOrDefault(Function(candidate) candidate.StartsWith("A")), "no matching employee found"
        )

        ' The last argument is an expression, not a list, so there is nothing to open up and the
        ' commas break straight away.
        Dim plain = Math.Max(_employees.Count * 1000, _employees.Count * 2000 + _employees.Count * 3000 - 4000)
    End Sub
End Class
