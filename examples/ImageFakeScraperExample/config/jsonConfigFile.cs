﻿namespace ImageFakeScraperExample.config
{
    internal class jsonConfigFile
    {
        public string Credential { get; set; }
        public double Sleep { get; set; }
        public string Pseudo { get; set; }
        public string domain_blacklist { get; set; }
        public string words_list { get; set;}
        public string words_done { get; set;}
        public string record_push { get; set;}
        public string jobs_last_index { get; set;}
        public string image_jobsPattern { get; set;}
    }
}