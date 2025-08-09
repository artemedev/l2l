using System.Collections.Generic;

namespace l2l_aggregator.Models
{
    public class ArmJobRequest
    {
        public string? userid { get; set; }
    }

    public class ArmJobRecord
    {
        public long DOCID { get; set; }//
        public long RESOURCEID { get; set; }//
        public long SERIESID { get; set; }//
        public long RES_BOXID { get; set; }//
        public int DOC_ORDER { get; set; }
        public string? DOCDATE { get; set; }
        public string? MOVEDATE { get; set; }//
        public string? BUHDATE { get; set; }//
        public long FIRMID { get; set; }
        public string? DOC_NUM { get; set; }
        public string? DEPART_NAME { get; set; }
        public string? RESOURCE_NAME { get; set; }
        public string? RESOURCE_ARTICLE { get; set; }
        public string? SERIES_NAME { get; set; }
        public string? RES_BOX_NAME { get; set; }
        public string? GTIN { get; set; }
        public string? EXPIRE_DATE_VAL { get; set; }
        public string? MNF_DATE_VAL { get; set; }
        public string? DOC_TYPE { get; set; }//
        public int AGREGATION_CODE { get; set; }
        public string? AGREGATION_TYPE { get; set; }
        public int CRYPTO_CODE_FLAG { get; set; }//
        public int ERROR_FLAG { get; set; }
        public int ERROR_DESCRIPTION { get; set; }
        public string? FIRM_NAME { get; set; }
        public int? QTY { get; set; }
        public int AGGR_FLAG { get; set; }
        public long UN_TEMPLATE { get; set; }
        public long UN_TEMPLATEID { get; set; }
        public long UN_RESERVE_DOCID { get; set; }
        public bool IsLast { get; internal set; }
    }

    public class ArmJobResponse
    {
        public List<ArmJobRecord> RECORDSET { get; set; } = new List<ArmJobRecord>();
    }

    public class ArmJobInfoRequest
    {
        public long docid { get; set; }
    }

    public class ArmJobInfoRecord
    {
        // Базовые поля (уже существующие)
        public long? DOCID { get; set; }
        public long? RESOURCEID { get; set; }
        public long? SERIESID { get; set; }
        public long? RES_BOXID { get; set; }
        public int? DOC_ORDER { get; set; }
        public string? DOCDATE { get; set; }
        public string? MOVEDATE { get; set; }
        public string? BUHDATE { get; set; }
        public long? FIRMID { get; set; }
        public string? DOC_NUM { get; set; }
        public string? DEPART_NAME { get; set; }
        public string? RESOURCE_NAME { get; set; }
        public string? RESOURCE_ARTICLE { get; set; }
        public string? SERIES_NAME { get; set; }
        public string? RES_BOX_NAME { get; set; }
        public string? GTIN { get; set; }
        public string? EXPIRE_DATE_VAL { get; set; }
        public string? MNF_DATE_VAL { get; set; }
        public string? DOC_TYPE { get; set; }
        public int? AGREGATION_CODE { get; set; }
        public string? AGREGATION_TYPE { get; set; }
        public short? CRYPTO_CODE_FLAG { get; set; }
        public string? FIRM_NAME { get; set; }
        public int? QTY { get; set; }
        public short? AGGR_FLAG { get; set; }
        public byte[]? UN_TEMPLATE { get; set; }
        public long? UN_TEMPLATEID { get; set; }
        public long? UN_RESERVE_DOCID { get; set; }
        public byte[]? UN_TEMPLATE_FR { get; set; }

        // Дополнительные поля из ARM_TASK
        public int? IN_BOX_QTY { get; set; } //колличество пачек в коробке
        public int? IN_INNER_BOX_QTY { get; set; }
        public short? INNER_BOX_FLAG { get; set; }
        public short? INNER_BOX_AGGR_FLAG { get; set; }
        public int? INNER_BOX_QTY { get; set; }
        public int? IN_PALLET_BOX_QTY { get; set; }
        public string? LAST_PACKAGE_LOCATION_INFO { get; set; }
        public short? PALLET_NOT_USE_FLAG { get; set; }
        public short? PALLET_AGGR_FLAG { get; set; }
        public long? AGREGATION_TYPEID { get; set; }
        public int? SERIES_SYS_NUM { get; set; }
        public int? LAYERS_QTY { get; set; } //колличество слоёв в коробке
        public int? LAYER_ROW_QTY { get; set; }
        public int? LAYER_ROWS_QTY { get; set; }
        public int? PACK_HEIGHT { get; set; }
        public int? PACK_WIDTH { get; set; }
        public int? PACK_LENGTH { get; set; }
        public int? PACK_WEIGHT { get; set; }
        public string? PACK_CODE_POSITION { get; set; }
        public long? BOX_TEMPLATEID { get; set; }
        public long? BOX_RESERVE_DOCID { get; set; }
        public byte[]? BOX_TEMPLATE { get; set; } //шаблон коробки
        public long? PALLETE_TEMPLATEID { get; set; }
        public long? PALLETE_RESERVE_DOCID { get; set; }
        public byte[]? PALLETE_TEMPLATE { get; set; }
        public long? INT_BOX_TEMPLATEID { get; set; }
        public long? INT_BOX_RESERVE_DOCID { get; set; }
        public byte[]? INT_BOX_TEMPLATE { get; set; }
        public string? LOAD_START_TIME { get; set; }
        public string? LOAD_FINISH_TIME { get; set; }
        public string? EXPIRE_DATE { get; set; }
        public string? MNF_DATE { get; set; }
    }

    public class ArmJobSgtinResponse
    {
        public List<ArmJobSgtinRecord> RECORDSET { get; set; } = new List<ArmJobSgtinRecord>();
    }

    public class ArmJobSsccResponse
    {
        public List<ArmJobSsccRecord> RECORDSET { get; set; } = new List<ArmJobSsccRecord>();
    }

    // Обновленная модель для SSCC (MARK_SSCC_CODE)
    public class ArmJobSsccRecord
    {
        public long SSCCID { get; set; }
        public string? SSCC_CODE { get; set; } // Новое поле из реальной структуры
        public long STATEID { get; set; }
        public int TYPEID { get; set; }
        public string? DISPLAY_BAR_CODE { get; set; }
        public string? SSCC { get; set; } // Новое поле из реальной структуры
        public string? CHECK_BAR_CODE { get; set; }
        public int ORDER_NUM { get; set; }
        public string? CODE_STATE { get; set; }

        // Дополнительные поля из реальной структуры таблицы
        public int QTY { get; set; }
        public long? PARENT_SSCCID { get; set; }

        // Удаленное поле SERIESID - его нет в реальной структуре таблицы
    }
    // Обновленная модель для SGTIN (MARK_UN_CODE)
    public class ArmJobSgtinRecord
    {
        public long? UNID { get; set; }
        //public string? BARCODE { get; set; } // Может быть сгенерирован из UN_CODE
        public long STATEID { get; set; }
        public string? UN_CODE { get; set; }
        public string? GS1FIELD91 { get; set; }
        public string? GS1FIELD92 { get; set; }
        public string? GS1FIELD93 { get; set; }
        //public string? UN_CODE_STATEID { get; set; }
        //public string? UN_CODE_STATE { get; set; }

        // Дополнительные поля из реальной структуры таблицы
        public long? PARENT_SSCCID { get; set; }
        public int? UN_TYPE { get; set; }
        public long? PARENT_UNID { get; set; }
        public int? QTY { get; set; }
    }

    public class ArmCountersResponse
    {
        public List<ArmCountersRecord> RECORDSET { get; set; } = new();
    }

    public class ArmCountersRecord
    {
        public long? STATEID { get; set; }
        public string? CODE { get; set; }
        public string? NAME { get; set; }
        public int? QTY { get; set; }
        public int? JOB_QTY { get; set; }
    }
}
