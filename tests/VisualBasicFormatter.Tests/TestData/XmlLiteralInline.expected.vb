Imports System.Xml.Linq

Module Roster
    Private _employees As List(Of Employee)

    Public Function Build() As XElement
        Dim xml =
            <employees>
                <%= From e In _employees
                    Select
                        <employee
                            id=<%= e.Id %>
                            department=<%= e.Department %>>
                            <name>
                                <%= e.Name %>
                            </name>
                            <salary>
                                <%= e.Salary %>
                            </salary>
                            <tags>
                                <%= String.Join(",", e.Tags) %>
                            </tags>
                        </employee>
                %>
            </employees>

        Return xml
    End Function

    Public Function Small() As XElement
        Return <a>
            <b/>
        </a>
    End Function
End Module
