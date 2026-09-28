Imports System.ComponentModel
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices

<Assembly: CLSCompliant(True)>

<Serializable>
Public Class Employee
    <NonSerialized>
    Private _cache As String

    <Obsolete("use FullName instead"), Browsable(False)>
    Public Property Name As String

    ' Two attributes, too long together: the list breaks at the comma, not behind the bracket.
    <Obsolete("this deprecation message is long enough to push the attribute list over the column limit"),
        Browsable(False)>
    Public Sub Reset()
        _cache = Nothing
    End Sub

    ' An attribute on a parameter stays on its line.
    Public Sub Write(<Out> ByRef count As Integer)
        count = 0
    End Sub

    <Category("Data")>
    Public ReadOnly Property Cache As String
        <DebuggerStepThrough>
        Get
            Return _cache
        End Get
    End Property
End Class

Public Module EmployeeExtensions
    <Extension>
    Public Function HasUsefulName(employee As Employee) As Boolean
        Return employee IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(employee.Name)
    End Function
End Module

Public Enum Level
    <Description("none")>
    None = 0
    <Description("some")>
    Some = 1
End Enum
