namespace AgOpenGPS
{
    public class CFeatureSettings
    {
        public CFeatureSettings()
        { }

        //public bool ;
        public bool isHeadlandOn { get; set; } = true;
        public bool isTramOn { get; set; } = false;
        public bool isBoundaryOn { get; set; } = true;
        public bool isBndContourOn { get; set; } = false;
        public bool isRecPathOn { get; set; } = false;
        public bool isABSmoothOn { get; set; } = false;
        public bool isHideContourOn { get; set; } = false;
        public bool isWebCamOn { get; set; } = false;
        public bool isOffsetFixOn { get; set; } = false;
        public bool isAgIOOn { get; set; } = true;
        public bool isContourOn { get; set; } = true;
        public bool isYouTurnOn { get; set; } = true;
        public bool isSteerModeOn { get; set; } = true;
        public bool isManualSectionOn { get; set; } = true;
        public bool isAutoSectionOn { get; set; } = true;
        public bool isCycleLinesOn { get; set; } = true;
        public bool isABLineOn { get; set; } = true;
        public bool isCurveOn { get; set; } = true;
        public bool isAutoSteerOn { get; set; } = true;
        public bool isUTurnOn { get; set; } = true;
        public bool isLateralOn { get; set; } = true;

        public CFeatureSettings(CFeatureSettings _feature)
        {
            isHeadlandOn = _feature.isHeadlandOn;
            isTramOn = _feature.isTramOn;
            isBoundaryOn = _feature.isBoundaryOn;
            isBndContourOn = _feature.isBndContourOn;
            isRecPathOn = _feature.isRecPathOn;

            isABSmoothOn = _feature.isABSmoothOn;
            isHideContourOn = _feature.isHideContourOn;
            isWebCamOn = _feature.isWebCamOn;
            isOffsetFixOn = _feature.isOffsetFixOn;
            isAgIOOn = _feature.isAgIOOn;

            isContourOn = _feature.isContourOn;
            isYouTurnOn = _feature.isYouTurnOn;
            isSteerModeOn = _feature.isSteerModeOn;

            isManualSectionOn = _feature.isManualSectionOn;
            isAutoSectionOn = _feature.isAutoSectionOn;
            isCycleLinesOn = _feature.isCycleLinesOn;
            isABLineOn = _feature.isABLineOn;
            isCurveOn = _feature.isCurveOn;

            isAutoSteerOn = _feature.isAutoSteerOn;
            isLateralOn = _feature.isLateralOn;
            isUTurnOn = _feature.isUTurnOn;
        }
    }
}