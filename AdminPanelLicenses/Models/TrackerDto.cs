namespace AdminPanelLicenses.Models
{
    public class TrackerDto
    {
        public int Id { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Model { get; set; }
        public string? Manufacturer { get; set; }
        public string? SerialNumber { get; set; }
        public int BatteryLevel { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsOnline { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsAvailableForAssignment { get; set; }
    }

    public class CustomerDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Plan { get; set; } = string.Empty;
        public int TrackerLimit { get; set; }
        public int FarmLimit { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? SubscriptionStart { get; set; }
        public DateTime? SubscriptionEnd { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public int ActiveTrackerCount { get; set; }
    }

    public class CustomerTrackerDto
    {
        public int Id { get; set; }
        public int TrackerId { get; set; }
        public int CustomerId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string TrackerName { get; set; } = string.Empty;
        public string? Model { get; set; }
        public int BatteryLevel { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsOnline { get; set; }
        public DateTime AssignedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? FarmName { get; set; }
        public string? AnimalName { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPlan { get; set; } = string.Empty;
    }

    public class AssignTrackerRequest
    {
        public int TrackerId { get; set; }
        public int CustomerId { get; set; }
    }

    public class UnassignTrackerRequest
    {
        public int CustomerTrackerId { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }

    public class TrackersListResponse
    {
        public bool Success { get; set; }
        public List<TrackerDto> Trackers { get; set; } = new();
        public bool CanAddMore { get; set; }
        public int CurrentCount { get; set; }
        public int MaxTrackers { get; set; }
    }

    public class CustomerTrackersResponse
    {
        public bool Success { get; set; }
        public List<CustomerTrackerDto> Trackers { get; set; } = new();
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool HasCustomer { get; set; }
    }
}
