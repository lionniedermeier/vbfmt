Imports System.Xml.Linq

Module MultiLineContent
    Sub Diagnostics()
        Assert.Equal(
            <text>
SRC.VB(7) : warning BC42104: message

        Console.WriteLine(x.ToString)
                          ~
</text>.Value.Trim(),
            actual
        )
    End Sub

    Sub Nested()
        Dim wrapped =
            <root>
                <text>
line with trailing space   
</text>
            </root>
    End Sub

    Sub CommentAndAttribute()
        Dim markup = <!-- line one   
line two -->
        Dim tagged = <x a="line one   
line two"/>
    End Sub

    Sub Interpolated()
        Dim flush = $"
line one   
line two"
        Dim indented = $"start
    line two   
end"
    End Sub

    Sub SingleLineIf()
        If True Then Console.WriteLine("line one   
line two")
    End Sub
End Module
