Imports System.Xml.Linq

' Elements with text content: the whitespace there belongs to the author and stays untouched.
Module Prose
    Public Function Paragraph(title As String) As XElement
        Return <document>
            <heading>
                <%= title %>
            </heading>
            <body>A paragraph with plenty of text, so the line exceeds the limit and still stays as is.</body>
        </document>
    End Function

    Public Function Mixed(name As String) As XElement
        Return <greeting>
                   Hello <%= name %>, glad you could make it.
               </greeting>
    End Function

    Public Function Entity() As XElement
        Return <spacer>&#x20;</spacer>
    End Function

    Public Function Preformatted() As XElement
        Return <pre xml:space="preserve">   two   columns
   and    one   more</pre>
    End Function
End Module
