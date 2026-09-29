Public Class Test
#Region "Test Region 1"
    Public Sub MethodA()
    End Sub
#End Region

#Region "Test Region"
    Public Sub MethodB()
    End Sub
#End Region

    Public Sub MethodC()
    End Sub

#Region "Test Region"
    Public Sub MethodD()
    End Sub
#End Region
End Class

Public Class Fields
    Private _a As Integer

#Region "Inner"
    Private _b As Integer
    Private _c As Integer
#End Region

    Private _d As Integer
End Class

Public Class Nested
#Region "Outer"
#Region "Inner"
    Public Sub Run()
    End Sub
#End Region
#End Region
End Class

Public Class Commented
    Private _a As Integer
    ' About the region
#Region "Commented"
    Private _b As Integer
#End Region
End Class

Namespace Tracker

#Region "Types"
    Public Class First
    End Class

    Public Class Second
    End Class
#End Region
End Namespace
