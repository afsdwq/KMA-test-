using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;

namespace 기상청DLL
{
    public class RegionMapper
    {
        private class RegionInfo
        {
            public string 행정구역 { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
        }

        private List<RegionInfo> regionList;

        public RegionMapper(string csvFilePath)
        {
            using (var reader = new StreamReader(csvFilePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Context.RegisterClassMap<RegionMap>();
                regionList = csv.GetRecords<RegionInfo>().ToList();
            }
        }

        // x, y 값으로 지역명 반환
        public string GetRegionName(int x, int y)
        {
            var match = regionList.FirstOrDefault(r => r.X == x && r.Y == y);
            return match?.행정구역 ?? "지역 정보 없음";
        }

        // CSV 열 매핑
        private sealed class RegionMap : CsvHelper.Configuration.ClassMap<RegionInfo>
        {
            public RegionMap()
            {
                Map(m => m.행정구역).Name("1단계", "행정구역");
                Map(m => m.X).Name("격자 X");
                Map(m => m.Y).Name("격자 Y");
            }
        }
    }
}
