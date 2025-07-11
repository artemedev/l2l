namespace DM_process_lib
{
    using DM_wraper_NS;
    public class DM_process()
    {
        MainProces _MP = new MainProces();
        public void Init(DM_recogn_wraper dM_recogn_wraper)
        {
            _DMP.init_readConfig();
            _DMP.updateWraper(dM_recogn_wraper);
            Console.WriteLine("DM_process - initialisation");
            //Подписались на событие
            dM_recogn_wraper.libStartShot += _MP.MP_StartShot;
            dM_recogn_wraper.libTakeShot += _MP.MP_TakeShot;
            dM_recogn_wraper.libStopShot += _MP.MP_StopShot;
            dM_recogn_wraper.libUpdateParams += _DMP.update_DMP;
            dM_recogn_wraper.libUpdatePrintPattern += _DMP.update_PP;
            dM_recogn_wraper.swStartOk += _MP.MP_StartShot;
            //dM_recogn_wraper.libGetCameras += _MP.GetCameras;
            //set parameters
            dM_recogn_wraper.initForLib(_DMP._dmp);
        }
    }
}