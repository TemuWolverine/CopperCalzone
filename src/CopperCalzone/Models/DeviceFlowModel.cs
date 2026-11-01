namespace CopperCalzone.Models;

public record DeviceFlowModel(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    int ExpiresIn,
    int Interval
);