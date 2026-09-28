Module IgnoreComment
    ' vbfmt-ignore
    ''' <summary>
  ''' This documentation and the method should not be formatted.
  ''' </summary>
  Public Sub MethodName(      )
          Dim unformatted =     ""
  End Sub

    Public Sub HandlesIgnoredStatement()
        Dim a = 1
        ' vbfmt-ignore
        Dim   b    =     2
        Dim c = 3
    End Sub

    ' vbfmt-ignore
    Public Sub HandlesTrailingComment()
            Dim x =    1
    End Sub ' trailing note

    Public Sub HandlesOrdinaryCommentAboveMarker()
        ' An ordinary comment stays normalized.
        ' vbfmt-ignore
        Dim   y   =    4
    End Sub

    Public Sub AfterIgnored()
        Dim z = 5
    End Sub
End Module
