Public Class LambdaBlocks

    Public Sub RunsInParallel()
        Try
            Parallel.ForEach(
                paramA,
                paramB,
                Sub(someParam)
                    ' Lambda content with several nested layers that are longer than printWidth
                    If someParam IsNot Nothing AndAlso someParam.Value > 0 Then
                        Compute(someParam)
                    End If
                End Sub
            )
        Catch ex As Exception
        End Try
    End Sub

    Public Sub CommentAboveEndSub()
        Register(
            paramA,
            Sub(someParam)
                Work(someParam)
                ' about to return
            End Sub
        )
    End Sub

    Public Sub CommentInsideANestedExpression()
        Register(
            paramA,
            paramB,
            Sub(someParam)
                Dim matches = someParam.Where(
                    Function(x) x.Value > 0 AndAlso ' inline note
                        x.Other < 10
                )
                Work(matches)
            End Sub
        )
    End Sub

    Public Sub DirectiveInsideTheBody()
        Register(paramA, Sub(someParam)
        #If DEBUG Then
                Work(someParam)
        #End If
            End Sub)
    End Sub

    Public Sub HangsAFunctionAssignment()
        Dim handler =
            Function(value As Integer)
                Return value * 2
            End Function
    End Sub

    Public Sub HangsASubAssignment()
        Dim g As Action =
            Sub(p)
                Work(p)
            End Sub
    End Sub

    Public Sub HangsAnAddHandler()
        AddHandler btn.Click,
            Sub(s, e)
                Work(s)
            End Sub
    End Sub

    Public Function KeepsAlignForReturn() As Action
        Return Sub()
                   Work()
               End Sub
    End Function

End Class
