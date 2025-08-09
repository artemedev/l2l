namespace l2l_aggregator.Models
{
    public class ArmDeviceRegistrationRequest
    {
        public string? NAME { get; set; }
        public string? MAC_ADDRESS { get; set; }
        public string? SERIAL_NUMBER { get; set; }
        public string? NET_ADDRESS { get; set; }
        public string? KERNEL_VERSION { get; set; }
        public string? HARDWARE_VERSION { get; set; }
        public string? SOFTWARE_VERSION { get; set; }
        public string? FIRMWARE_VERSION { get; set; }
        public string? DEVICE_TYPE { get; set; }
    }

    public class ArmDeviceRegistrationResponse
    {
        public string? DEVICEID { get; set; }
        public string? DEVICE_NAME { get; set; }
        public string? LICENSE_DATA { get; set; }
        public string? SETTINGS_DATA { get; set; }
    }



    public class UserAuthResponse
    {
        public string? USERID { get; set; }
        public string? USER_NAME { get; set; }
        public string? PERSONID { get; set; }
        public string? PERSON_NAME { get; set; }
        public string? PERSON_DELETE_FLAG { get; set; }
        public string? AUTH_OK { get; set; }
        public string? ERROR_TEXT { get; set; }
        public string? NEED_CHANGE_FLAG { get; set; }
        public string? EXPIRATION_DATE { get; set; }
        public string? REC_TYPE { get; set; }
    }
}
