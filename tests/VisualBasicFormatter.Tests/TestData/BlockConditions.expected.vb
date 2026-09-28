Module BlockConditions
    Public Function Classify(ByVal candidate As Contract) As String
        If candidate.IsActive AndAlso
                candidate.HasValidSignature AndAlso
                candidate.RemainingTermInMonths > 0 AndAlso
                Not candidate.IsSuspended Then
            Return "A"
        ElseIf candidate.IsPending AndAlso
                candidate.HasValidSignature AndAlso
                candidate.RemainingTermInMonths >= 0 AndAlso
                Not candidate.IsSuspended Then
            Return "B"
        Else
            Return "C"
        End If
    End Function

    Public Sub CountActive(ByVal candidate As Contract)
        While candidate.IsActive AndAlso
                candidate.HasValidSignature AndAlso
                candidate.RemainingTermInMonths > 0 AndAlso
                Not candidate.IsSuspended
            candidate.Counter += 1
        End While

        Do While candidate.IsActive AndAlso
                candidate.HasValidSignature AndAlso
                candidate.RemainingTermInMonths > 0 AndAlso
                Not candidate.IsSuspended
            candidate.Counter += 1
        Loop

        Do Until candidate.IsSuspended OrElse
                candidate.HasExpired OrElse
                candidate.RemainingTermInMonths <= 0 OrElse
                Not candidate.HasValidSignature
            candidate.Counter += 1
        Loop

        Do
            candidate.Counter += 1
        Loop While candidate.IsActive AndAlso
                candidate.HasValidSignature AndAlso
                candidate.RemainingTermInMonths > 0 AndAlso
                Not candidate.IsSuspended

        Do
            candidate.Counter += 1
        Loop Until candidate.IsSuspended OrElse
                candidate.HasExpired OrElse
                candidate.RemainingTermInMonths <= 0 OrElse
                Not candidate.HasValidSignature
    End Sub

    Public Function Mixed(ByVal candidate As Contract) As Boolean
        If candidate.IsSuspended OrElse
                candidate.HasValidSignature AndAlso
                candidate.RemainingTermInMonths > 0 AndAlso
                Not candidate.HasExpiredAlready Then
            Return True
        End If

        Return False
    End Function

    Public Function InsideCall(ByVal candidate As Contract) As Boolean
        If Evaluate(
            candidate.PrimaryHolderName, candidate.SecondaryHolderName, candidate.BranchName, candidate.ProductName
        ) Then
            Return True
        End If

        Return False
    End Function

    Public Function OnAChain(ByVal candidate As Contract) As Boolean
        If candidate.
                ResolvePrimaryHolder().
                LoadLinkedAccount().
                FetchActiveSubscription().
                IsWithinTheRenewalGracePeriodRightNow() Then
            Return True
        End If

        Return False
    End Function
End Module
