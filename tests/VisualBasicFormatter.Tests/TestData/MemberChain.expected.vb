Module Selection
    Public Sub Collect()
        Dim companies = State.Companies.Values.
            Where(AddressOf FilterDivision).
            Where(Function(g) FilterLegalForm(g.LegalForm)).
            Where(Function(g) Not visited.Contains(g)).
            Where(
                Function(g) g.MergerId > 0 AndAlso
                    State.Companies.ContainsKey(g.MergerId) AndAlso
                    visited.Contains(State.Companies(g.MergerId))
            )
    End Sub

    Public Sub CollectSingleDot()
        Dim isEligible = candidate.PrimaryAccountHolderRecord.LinkedSubscriptionRegistrationEntry.
            CurrentRenewalConfigurationSettings.
            EvaluatesEligibilityForAutomaticRenewalRightNow(candidate.PrimaryHolderName, candidate.SecondaryHolderName)
    End Sub
End Module
