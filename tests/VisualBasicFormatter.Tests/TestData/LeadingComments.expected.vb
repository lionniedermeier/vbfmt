Module LeadingComments

    Public Sub HandlesAddHandler()
        ' Leading Trivia
        AddHandler SendButton.Click, AddressOf ExecuteSend
    End Sub

    Public Sub HandlesShortChain()
        ' Leading Trivia
        app.Sheets(xlSheet.Name).Delete()
    End Sub

    Public Sub HandlesOverrunChain()
        ' A comment above a statement that genuinely overruns still leaves it wrapped, at the columns the uncommented statement would wrap at.
        Dim companies = State.Companies.Values.
            Where(AddressOf FilterDivision).
            Where(Function(g) FilterLegalForm(g.LegalForm)).
            Where(Function(g) Not visited.Contains(g))
    End Sub

    Public Sub HandlesNesting(ByVal items As List(Of Integer))
        If items.Count > 0 Then
            ' A comment above a statement nested inside a block.
            Process(items)
        End If

        For Each item In items
            ' A comment above a statement nested inside a loop.
            item.Report()
        Next
    End Sub

    Public Sub HandlesBlankLineAbove()
        Dim a = 1

        ' A blank line above the comment is preserved above it, not swallowed.
        Dim b = 2
    End Sub

    Public Sub HandlesCommentedFooter()
        Dim value = 1
        ' A comment above the block's own closing statement.
    End Sub

End Module
