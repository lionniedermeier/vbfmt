Imports System.Xml.Linq

Module Tags
    Public Function Wide(employee As Employee) As XElement
        Return <employee
            id=<%= employee.Id %>
            name=<%= employee.Name %>
            age=<%= employee.Age %>
            salary=<%= employee.Salary %>
            status=<%= employee.Status %>/>
    End Function

    Public Function Narrow() As XElement
        Return <link rel="stylesheet" href="site.css"/>
    End Function

    Public Function Container(employee As Employee) As XElement
        Return <person
            id=<%= employee.Id %>
            department=<%= employee.Department %>
            location=<%= employee.Location %>>
            <tags>
                <%= String.Join(",", employee.Tags) %>
            </tags>
        </person>
    End Function
End Module
