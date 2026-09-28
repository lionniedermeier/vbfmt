Module SelectCaseInline
    Public Function Indent(ByVal depth As Integer) As String
        Select Case depth
            Case 0 : Return ""
            Case 1 : Return "    "
            Case 2 : Return "        "
            Case 3 : Return "            "
        End Select
    End Function

    Public Function IndentWrapped(ByVal depth As Integer) As String
        Select Case depth
            Case 0
                Return ""
            Case 1
                Return "    "
        End Select
    End Function

    Public Function DescribeVeryVeryVeryVeryLongOverload(ByVal depth As Integer) As String
        Select Case depth
            Case 0
                Return "SomeVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryLongLongLongLongLongLongResultX"
        End Select
    End Function

    Public Sub CompactAssignments(ByVal kind As Integer)
        Dim a As Integer
        Dim b As Integer

        Select Case kind
            Case 0
                a = 1
                b = 2
        End Select
    End Sub

    Public Function DescribeElse(ByVal kind As Integer) As String
        Select Case kind
            Case 0 : Return "zero"
            Case Else : Return "other"
        End Select
    End Function

    Public Sub CompactBlock(ByVal kind As Integer)
        Select Case kind
            Case 0
                If kind = 0 Then
                    Console.WriteLine("zero")
                End If
        End Select
    End Sub

    Public Function DescribeCommented(ByVal kind As Integer) As String
        Select Case kind
            Case 0
                ' A comment above the statement forces the expanded body.
                Return "zero"
        End Select
    End Function

    Public Function DescribeWideCase(ByVal kind As SyntaxKind) As Boolean
        Select Case kind
            Case SyntaxKind.CBoolKeyword,
                    SyntaxKind.CDateKeyword,
                    SyntaxKind.CDblKeyword,
                    SyntaxKind.CSByteKeyword,
                    SyntaxKind.CByteKeyword,
                    SyntaxKind.CCharKeyword,
                    SyntaxKind.CShortKeyword
                Return True
        End Select
    End Function
End Module
