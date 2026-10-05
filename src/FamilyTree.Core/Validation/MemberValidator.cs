using FamilyTree.Core.DTOs;

namespace FamilyTree.Core.Validation;

public sealed class MemberValidator(TimeProvider clock)
{
    public void Validate(SaveMemberRequest request)
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            errors.Add(new("InvalidName", "Name", "Enter a name between 1 and 200 characters."));
        if (request.DateOfBirth > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
            errors.Add(new("FutureBirthDate", "DateOfBirth", "Birth date cannot be in the future."));
        if (!Enum.IsDefined(request.Gender)) errors.Add(new("InvalidGender", "Gender", "Choose a supported gender."));
        if (!Enum.IsDefined(request.LifeStatus)) errors.Add(new("InvalidLifeStatus", "LifeStatus", "Choose a supported life status."));
        if (!Enum.IsDefined(request.MaritalStatus)) errors.Add(new("InvalidMaritalStatus", "MaritalStatus", "Choose a supported marital status."));
        if (errors.Count > 0) throw new DomainValidationException(errors.ToArray());
    }
}
