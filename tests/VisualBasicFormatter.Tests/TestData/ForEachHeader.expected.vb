Module ForEachHeader
    Public Sub OnAChain(ByVal elementStore As ElementStore)
        For Each element In elementStore.
                ResolvePrimaryElements().
                LoadLinkedElements().
                FetchActiveElements().
                WithinCurrentScope()
            element.Activate()
        Next
    End Sub

    Public Sub ShortCollection(ByVal items As IEnumerable(Of Element))
        For Each item In items
            item.Activate()
        Next
    End Sub

    Public Sub InsideCall(ByVal elementStore As ElementStore)
        For Each element In Evaluate(
            elementStore.PrimaryHolderName,
            elementStore.SecondaryHolderName,
            elementStore.BranchName,
            elementStore.ProductName
        )
            element.Activate()
        Next
    End Sub

    Public Sub NumericBound(ByVal elementStore As ElementStore)
        For i = 0 To elementStore.
            ResolvePrimaryElements().
            LoadLinkedElements().
            FetchActiveElements().
            CountWithinCurrentScope()
            elementStore.Activate(i)
        Next
    End Sub
End Module
