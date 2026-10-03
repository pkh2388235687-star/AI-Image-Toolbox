using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QQImageSwitch
{
    static class InspectionTests
    {
        internal const string DemoPrompt="{\"3\":{\"class_type\":\"KSampler\",\"inputs\":{\"positive\":[\"6\",0],\"negative\":[\"7\",0],\"model\":[\"4\",0],\"latent_image\":[\"5\",0],\"cfg\":7,\"seed\":1234567890123456,\"steps\":28,\"sampler_name\":\"dpmpp_2m\",\"scheduler\":\"karras\",\"denoise\":1}},\"4\":{\"class_type\":\"CheckpointLoaderSimple\",\"inputs\":{\"ckpt_name\":\"example-model.safetensors\"}},\"5\":{\"class_type\":\"EmptyLatentImage\",\"inputs\":{\"width\":1024,\"height\":1536,\"batch_size\":1}},\"6\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"text\":\"masterpiece, mountain lake, soft light\",\"clip\":[\"4\",1]}},\"7\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"text\":\"low quality, blurry, watermark\",\"clip\":[\"4\",1]}}}";
        public static InspectionResult DemoResult()
        {
            var result=new InspectionResult {Parameters="masterpiece, mountain lake, soft light\nNegative prompt: low quality, blurry, watermark\nSteps: 28, Sampler: DPM++ 2M, Schedule type: Karras, CFG scale: 7, Seed: 123456, Size: 1024x1536, Model: example-model, Clip skip: 2",PromptJson=DemoPrompt};
            result.WebUI.AddRange(Inspection.ParseWebUI(result.Parameters));result.Comfy.AddRange(Inspection.ParseGraph(Inspection.Json().DeserializeObject(DemoPrompt) as Dictionary<string,object>));return result;
        }
        public static void Run(string dir)
        {
            Directory.CreateDirectory(dir);var data=DemoResult();
            if(data.Comfy.Count!=1||data.Comfy[0].Fields[0].Value!="masterpiece, mountain lake, soft light"||data.Comfy[0].Fields[1].Value!="low quality, blurry, watermark")throw new Exception("Positive and negative graph links not distinguished");
            if(!data.Comfy[0].Fields.Any(f=>f.Value=="1234567890123456"))throw new Exception("Seed precision lost");
            var fields=Inspection.ParseWebUI("a cat, (Steps: 12), red\nline two\nNegative prompt: blue, blurry\nSteps: 30, Sampler: Euler a, CFG scale: 6.5, Seed: 42, Model: \"a, b\", Hashes: {\"lora\": \"a,b\"}, Foo: bar");
            if(fields[0].Value!="a cat, (Steps: 12), red\nline two"||fields[1].Value!="blue, blurry"||fields.Find(f=>f.Name=="模型 Model").Value!="a, b"||!fields.Any(f=>f.Name=="Foo"&&f.Value=="bar"))throw new Exception("WebUI prompt, quoted comma, JSON or extra parameter parsing failed");
            File.WriteAllText(Path.Combine(dir,"inspection-tests.txt"),"PASS: separate positive/negative graph links, exact large seed, multiline WebUI prompts, quoted commas, nested JSON, arbitrary extra parameter fields.");
        }
    }
}
