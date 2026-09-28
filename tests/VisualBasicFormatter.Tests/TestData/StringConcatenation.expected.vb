Module Concatenation
    Public Sub Queries(ByVal identifier As Integer, ByVal name As String)
        Dim sqlQueryExpressionAsString = "select id, name, address, salary" & "from employees"

        Dim longSqlQueryExpressionAsString =
            "select id, name, address, salary" &
            "from employees" &
            "where salary > 80000"

        Dim deliberatelyWrappedQuery =
            "select id, name, address, salary" &
            "from employees"

        Dim unevenlyWrappedQuery =
            "select a, b" &
            " from t" & vbCrLf &
            " where x = " & identifier

        Dim continuedWithUnderscore =
            "first part " &
            "second part"

        Dim interpolatedFragments =
            $"id={identifier}" &
            $" name={name}"

        Dim concatInsideInterpolation = $"{name & identifier}"

        Dim tooLongForItsLine =
            "a rather long opening fragment of the message" & name & " and a considerably longer trailing fragment"

        Dim stillTooLongOneLevelIn =
            "a rather long opening fragment of the message text that keeps going" &
            name &
            " and a considerably longer trailing fragment of it"
    End Sub

    Public Function Describe(ByVal name As String) As String
        Return "alpha" &
            name
    End Function

    Public Sub Report(ByVal name As String, ByVal other As String)
        Log(
            "context",
            "alpha" &
                name,
            other
        )

        Report(name & other)
    End Sub

    Public Sub Append(ByRef target As String, ByVal name As String)
        target &= "alpha" &
            name
    End Sub
End Module
