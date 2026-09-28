Public Class ClosingComments

    Public Function CommentDedentBug()
        If A Then

            ' Comment
        End If
        ' Comment
    End Function

    Public Sub EmptyBodyComment()
        ' TODO
    End Sub

    Public Sub CommentBeforeEndIfAfterElse()
        If A Then
            DoSomething()
        Else
            DoSomethingElse()
            ' Comment before End If
        End If
    End Sub

    Public Sub CommentAboveElseStaysPut()
        If A Then
            DoSomething()
        ' Comment above Else
        Else
            DoSomethingElse()
        End If
    End Sub

    Public Sub CommentBeforeEndTryAfterFinally()
        Try
            DoSomething()
        Finally
            Cleanup()
            ' Comment before End Try
        End Try
    End Sub

    Public Sub CommentBeforeNext()
        For Each item In items
            Process(item)
            ' Comment before Next
        Next
    End Sub

    Public Sub CommentBeforeLoop()
        Do
            Process()
            ' Comment before Loop
        Loop While condition
    End Sub

    Public Sub CommentBeforeEndSelect()
        Select Case value
            Case 1
                DoOne()
            ' Comment before End Select
        End Select
    End Sub

    Public Sub CommentBeforeLambdaEndFunction()
        Dim f =
            Function(x As Integer) As Integer
                Return x
                ' Comment before lambda End Function
            End Function
    End Sub

#Region "Trailing region"
    Public Sub InRegion()
        DoSomething()
    End Sub

    ' Comment before End Region
#End Region
    ' Comment before End Class
End Class
