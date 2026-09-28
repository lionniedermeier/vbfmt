Module ParenthesizedConditions
    Public Function Classify(ByVal candidate As Contract) As String
        If (candidate.IsActive AndAlso candidate.HasValidSignature AndAlso candidate.RemainingTermInMonths > 0) Then
            Return "A"
        ElseIf (candidate.IsPending AndAlso
            candidate.HasValidSignature AndAlso
            candidate.RemainingTermInMonths >= 0
        ) Then
            Return "B"
        Else
            Return "C"
        End If
    End Function

    Public Function ClassifyWide(ByVal candidate As Contract) As String
        If (candidate.IsActive AndAlso
            candidate.HasValidSignature AndAlso
            candidate.RemainingTermInMonths > 0 AndAlso
            Not candidate.IsSuspended
        ) Then
            Return "A"
        End If

        Return "C"
    End Function

    Public Function Negated(ByVal candidate As Contract) As Boolean
        If Not (candidate.IsSuspended AndAlso candidate.HasExpired AndAlso candidate.RemainingTermInMonths <= 0) Then
            Return True
        End If

        Return False
    End Function

    Public Sub CountActive(ByVal candidate As Contract)
        While (candidate.IsActive AndAlso candidate.HasValidSignature AndAlso candidate.RemainingTermInMonths > 0)
            candidate.Counter += 1
        End While

        Do While (candidate.IsActive AndAlso candidate.HasValidSignature AndAlso candidate.RemainingTermInMonths > 0)
            candidate.Counter += 1
        Loop

        Do
            candidate.Counter += 1
        Loop Until (candidate.IsSuspended OrElse candidate.HasExpired OrElse candidate.RemainingTermInMonths <= 0)
    End Sub

    Public Function PartlyParenthesized(ByVal candidate As Contract) As Boolean
        If (candidate.IsActive AndAlso candidate.HasValidSignature) AndAlso
                candidate.RemainingTermInMonths > 0 AndAlso
                Not candidate.IsSuspended Then
            Return True
        End If

        Return False
    End Function

    Public Function Fits(ByVal a As Boolean, ByVal b As Boolean) As Boolean
        If (a AndAlso b) Then
            Return True
        End If

        Return False
    End Function

    Public Function InsideCall(ByVal candidate As Contract) As Boolean
        If (Evaluate(
            candidate.PrimaryHolderName, candidate.SecondaryHolderName, candidate.BranchName, candidate.ProductName
        )) Then
            Return True
        End If

        Return False
    End Function

    Public Function NegatedCall(ByVal candidate As Contract) As Boolean
        If (Not Evaluate(
            candidate.PrimaryHolderName, candidate.SecondaryHolderName, candidate.BranchName, candidate.ProductName
        )) Then
            Return True
        End If

        Return False
    End Function

    Public Function OperatorEndingInCall(ByVal candidate As Contract) As Boolean
        If (candidate.IsActive AndAlso
            Evaluate(candidate.PrimaryHolderName, candidate.SecondaryHolderName, candidate.BranchName)
        ) Then
            Return True
        End If

        Return False
    End Function
End Module
