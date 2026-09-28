Public Class Tests
    Inherits Ancestor
    Implements InterfaceA__________________,
        InterfaceB__________________,
        InterfaceC__________________,
        InterfaceD__________________

    Public Function Compare(
        left As Widget, right As Widget
    ) As Integer Implements IComparer______________.Compare,
            IWidgetOrdering______________.Compare,
            IRankingStrategy______________.Compare
        Return 0
    End Function

    Public ReadOnly Property Total As Integer Implements ITotals__________.Total,
            IStatistics________.Total,
            ISummary___________.Total
        Get
            Return 0
        End Get
    End Property
End Class

Public Class SingleWorker
    Implements IWorker

    Public Sub Run() Implements IWorker.Run
    End Sub
End Class
