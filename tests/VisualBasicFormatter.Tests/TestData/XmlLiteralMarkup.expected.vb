Imports System.Xml.Linq
Imports <xmlns:ns="urn:example">

' Nodes that are not an element, and the axes that access them.
Module Markup
    Public Function Sections() As XElement
        Return <root>
            <!-- a remark -->
            <?process this?>
            <![CDATA[ raw < text ]]>
            <empty/>
        </root>
    End Function

    Public Function Namespaced() As XElement
        Return <ns:excerpt
            ns:kind="short">
            <ns:line/>
        </ns:excerpt>
    End Function

    Public Function Empty() As XElement
        Return <empty></empty>
    End Function

    Public Function Axes(document As XElement) As IEnumerable(Of String)
        Return From item In document.<entry> Select item.@name
    End Function
End Module
