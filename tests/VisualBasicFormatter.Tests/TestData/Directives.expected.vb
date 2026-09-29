Module Switches
#Region "Setup"
    Public Sub Configure()
#If DEBUG Then
        Trace.Listeners.Add(New ConsoleTraceListener())
#Else
        Trace.Listeners.Clear()
#End If
    End Sub
#End Region
End Module
