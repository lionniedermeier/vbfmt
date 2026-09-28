Public Class Form1
    Private WithEvents A________ As Button
    Private WithEvents B_________ As Button
    Private WithEvents C_________ As Button
    Private WithEvents D_________ As Button
    Private WithEvents E_________ As Button
    Private WithEvents F_________ As Button
    Private WithEvents G_________ As Button

    Private Sub Handler(
        sender As Object, e As EventArgs
    ) Handles A________.Click,
            B_________.Click,
            C_________.Click,
            D_________.Click,
            E_________.Click,
            F_________.Click,
            G_________.Click
        DoSomething()
    End Sub

    Private Sub NoParameterHandler() Handles A________.Click,
            B_________.Click,
            C_________.Click,
            D_________.Click,
            E_________.Click,
            F_________.Click,
            G_________.Click
        DoSomething()
    End Sub

    Private Sub OneParameterHandler(
        sender As Object
    ) Handles A________.Click,
            B_________.Click,
            C_________.Click,
            D_________.Click,
            E_________.Click,
            F_________.Click,
            G_________.Click
        DoSomething()
    End Sub

    Private Sub ShortHandler(sender As Object, e As EventArgs) Handles A________.Click, B_________.Click
        DoSomething()
    End Sub
End Class
