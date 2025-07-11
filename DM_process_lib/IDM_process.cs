using DM_wraper_NS;

namespace DM_process_lib
{
    internal interface IDM_process
    {
    }

    /// <summary>
    /// Class with configuration for software [STATIC]
    /// </summary>
    public static class _DMP
    {
        public static recogn_params _dmp { get; private set; }

        public static DM_recogn_wraper _dM_recogn_wraper { get; private set; }

        public static string _pathToPrintPattern { get; private set; }
        public static void updateWraper(DM_recogn_wraper dM_recogn_wraper)
        {
            _dM_recogn_wraper = dM_recogn_wraper;
        }
        public static void update_DMP()
        {
            _dmp = _dM_recogn_wraper.GetParams();
        }

        public static void update_PP()
        {
            _pathToPrintPattern = _dM_recogn_wraper.GetXMLPrintPattern();
        }

        public static void init_readConfig()
        {
            //TODO make read JSON or TOML
            recogn_params newDMP = new recogn_params();
            //ok
            newDMP.countOfDM = 100;
            newDMP.pixInMM = 10;
            _dmp = newDMP;
        }
    }
}
