using Uniconta.API.DebtorCreditor;
using UnicontaClient.Models;
using Uniconta.ClientTools;
using Uniconta.ClientTools.Controls;
using Uniconta.ClientTools.DataModel;
using Uniconta.ClientTools.Page;
using Uniconta.Common;
using Uniconta.DataModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Uniconta.ClientTools.Util;

using Uniconta.API.GeneralLedger;
using System.Text;
using UnicontaClient.Utilities;
using System.ComponentModel.DataAnnotations;
using UnicontaClient.Controls.Dialogs;
using DevExpress.Xpf.Grid;
using System.ComponentModel;

using UnicontaClient.Pages;
namespace UnicontaClient.Pages.CustomPage
{
    public class SerialToOrderlineGrid : CorasauDataGridClient
    {
        public override Type TableType { get { return typeof(SerialToOrderLineClient); } }
        public override bool SingleBufferUpdate { get { return false; } }
        public override bool Readonly { get { return false; } }

        internal InvItem invItemMaster;
        internal DCOrderLine dcorderlineMaster;

        public override void SetDefaultValues(UnicontaBaseEntity dataEntity, int selectedIndex)
        {
            var newRow = (SerialToOrderLineClient)dataEntity;
            newRow.Mark = true;
            newRow._Qty = (invItemMaster._UseSerial) ? 1d : dcorderlineMaster._Qty;
            newRow._QtyMarked = newRow._Qty;
            if (dcorderlineMaster._Warehouse != null)
            {
                newRow._Warehouse = dcorderlineMaster._Warehouse;
                newRow._Location = dcorderlineMaster._Location;
            }
        }
    }
    public partial class SerialToOrderLinePage : GridBasePage
    {
        DCOrderLineClient dcorderlineMaster;
        InvItem invItemMaster;
        Company Comp;
        SQLCache itemCache;

        public SerialToOrderLinePage(SynchronizeEntity syncEntity)
           : base(syncEntity, false)
        {
            InitPage(syncEntity.Row);
        }

        public SerialToOrderLinePage(UnicontaBaseEntity master)
            : base(null)
        {
            InitPage(master);
        }

        void InitPage(UnicontaBaseEntity master)
        {
            InitializeComponent();
            dcorderlineMaster = master as DCOrderLineClient;
            Comp = api.CompanyEntity;
            itemCache = Comp.GetCache(typeof(InvItem));
            invItemMaster = itemCache.Get(dcorderlineMaster._Item) as InvItem;
            SetRibbonControl(localMenu, dgLinkedGrid);
            dgLinkedGrid.invItemMaster = invItemMaster;
            dgLinkedGrid.dcorderlineMaster = dcorderlineMaster;
            dgLinkedGrid.api = api;
            dgUnlinkedGrid.api = api;
            dgLinkedGrid.BusyIndicator = busyIndicator;
            dgUnlinkedGrid.BusyIndicator = busyIndicator;
            dgLinkedGrid.SelectedItemChanged += DgLinkedGrid_SelectedItemChanged;
            localMenu.OnItemClicked += localMenu_OnItemClicked;
            if (dcorderlineMaster.__DCType() != 2)
                RemoveMenuItem();
            if (Comp.Warehouse)
            {
                warehouse = Comp.GetCache(typeof(InvWarehouse));
            }
            ribbonControl.lowerSearchGrid = dgUnlinkedGrid;
            ribbonControl.UpperSearchNullText = Uniconta.ClientTools.Localization.lookup("Link");
            ribbonControl.LowerSearchNullText = Uniconta.ClientTools.Localization.lookup("Unlinked");
        }

        protected override void SyncEntityMasterRowChanged(UnicontaBaseEntity args)
        {
            if (args is DebtorOrderLineClient)
            {
                var orderLine = args as DebtorOrderLineClient;
                if (orderLine._Item == null)
                    return;
                var argsArray = new object[1];
                argsArray[0] = args;
                globalEvents.OnRefresh(NameOfControl, argsArray);
            }
            if (args is CreditorOrderLineClient)
            {
                var orderLine = args as DCOrderLineClient;
                if (orderLine._Item == null)
                    return;
                var item = (InvItem)itemCache.Get(orderLine._Item);
                if (!item._UseSerialBatch)
                    return;
                var argsArray = new object[1];
                argsArray[0] = args;
                globalEvents.OnRefresh(NameOfControl, argsArray);
            }
            SetupMaster(args);
            SetHeader(args);
            InitQuery();
        }

        void SetupMaster(UnicontaBaseEntity args)
        {
            dcorderlineMaster = args as DCOrderLineClient;
            invItemMaster = itemCache.Get(dcorderlineMaster._Item) as InvItem;
            dgLinkedGrid.invItemMaster = invItemMaster;
            dgLinkedGrid.dcorderlineMaster = dcorderlineMaster;
        }

        void SetHeader(UnicontaBaseEntity args)
        {
            string header = null;
            var orderLine = args as DCOrderLineClient;
            if(orderLine!= null)
                header = string.Format("{0}:{1}/{2},{3}", Uniconta.ClientTools.Localization.lookup("SerialBatchNumbers"), orderLine.OrderRowId, orderLine._Item, orderLine.RowId);
            if (header != null)
                SetHeader(header);
        }

        protected override async void LoadCacheInBackGround()
        {
            if (warehouse == null)
                warehouse = await api.CompanyEntity.LoadCache(typeof(Uniconta.DataModel.InvWarehouse), api).ConfigureAwait(false);
        }

        private void DgLinkedGrid_SelectedItemChanged(object sender, SelectedItemChangedEventArgs e)
        {
            var oldSelectedItem = e.OldItem as SerialToOrderLineClient;
            if (oldSelectedItem != null)
                oldSelectedItem.PropertyChanged -= dgLinkedGrid_PropertyChanged;

            var selectedItem = e.NewItem as SerialToOrderLineClient;
            if (selectedItem != null)
                selectedItem.PropertyChanged += dgLinkedGrid_PropertyChanged;
        }

        private void dgLinkedGrid_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var rec = sender as SerialToOrderLineClient;
            switch (e.PropertyName)
            {
                case "Warehouse":
                    var selected = (InvWarehouse)warehouse?.Get(rec._Warehouse);
                    SetLocation(selected, rec);
                    break;
                case "Location":
                    if (string.IsNullOrEmpty(rec._Warehouse))
                        rec._Location = null;
                    break;
            }
        }

        void RemoveMenuItem()
        {
            RibbonBase rb = (RibbonBase)ribbonControl.DataContext;
            UtilDisplay.RemoveMenuCommand(rb, new string[] { "AddRow", "CopyRow", "DeleteRow" });
        }

        public  async override Task InitQuery()
        {
            dgLinkedGrid.UpdateMaster(dcorderlineMaster as UnicontaBaseEntity);
            await dgLinkedGrid.Filter(null);
            if (dcorderlineMaster._Qty < 0)
            {
                // we select all, since it is a credit note.
                dgUnlinkedGrid.UpdateMaster(invItemMaster);
            }
            else
            {
                // We only select opens
                var mast = new InvSerieBatchOpen();
                mast.SetMaster(invItemMaster);
                dgUnlinkedGrid.UpdateMaster(mast);
            }
            dgUnlinkedGrid.Filter(null);
        }

        public override void AssignMultipleGrid(List<Uniconta.ClientTools.Controls.CorasauDataGrid> gridCtrls)
        {
            gridCtrls.Add(dgLinkedGrid);
            gridCtrls.Add(dgUnlinkedGrid);
        }

        private void localMenu_OnItemClicked(string ActionType)
        {
            switch (ActionType)
            {
                case "AddRow":
                    dgLinkedGrid.AddRow();
                    break;
                case "CopyRow":
                    dgLinkedGrid.CopyRow();
                    var newRow = dgLinkedGrid.CurrentItem as SerialToOrderLineClient;
                    if (newRow != null)
                        newRow.Mark = true;
                    break;
                case "SaveGrid":
                    saveGrid();
                    break;
                case "DeleteRow":
                    dgLinkedGrid.DeleteRow();
                    break;
                case "Link":
                    LinkRows(false);
                    break;
                case "Unlink":
                    UnlinkRows();
                    break;
                case "SaveAndClose":
                    SaveExit();
                    break;
                default:
                    gridRibbon_BaseActions(ActionType);
                    break;
            }
        }

        protected override async Task<ErrorCodes> saveGrid()
        {
            var res = await dgLinkedGrid.SaveData();
            LinkRows(true);
            return res;
        }

        bool saveAndExit = false;
        async private void SaveExit()
        {
            await dgLinkedGrid.SaveData();
            saveAndExit = true;
            await LinkRows(true);
            dockCtrl?.CloseDockItem();
        }

        private async void UnlinkRows()
        {
            dgLinkedGrid.SelectedItem = null;
            dgUnlinkedGrid.SelectedItem = null;
            var dcolSerieBatchList = new List<DCOrderLineSerieBatch>();
            var linkedRows = dgLinkedGrid.ItemsSource as List<SerialToOrderLineClient>;
            if (linkedRows == null)
            {
                dcorderlineMaster.SerieBatchMarked = false;
                return;
            }
            bool AllRemoved = true;
            foreach (var row in linkedRows)
            {
                if (row.Mark)
                {
                    var olSerieBatch = new DCOrderLineSerieBatch();
                    olSerieBatch.SetMaster(dcorderlineMaster as UnicontaBaseEntity);
                    olSerieBatch.SetMaster(row);
                    olSerieBatch._Qty = row._QtyMarked;
                    dcolSerieBatchList.Add(olSerieBatch);
                }
                else
                    AllRemoved = false;
            }
            var err = await api.Delete(dcolSerieBatchList);
            if (err != ErrorCodes.Succes)
                UtilDisplay.ShowErrorCode(err);
            else
            {
                InitQuery();
                if (AllRemoved)
                    dcorderlineMaster.SerieBatchMarked = false;
            }
        }

        private async Task LinkRows(bool added)
        {
            dgLinkedGrid.SelectedItem = null;
            dgUnlinkedGrid.SelectedItem = null;
            List<SerialToOrderLineClient> unlinkedRows;
            if (added)
                unlinkedRows = dgLinkedGrid.ItemsSource as List<SerialToOrderLineClient>;
            else
                unlinkedRows = dgUnlinkedGrid.ItemsSource as List<SerialToOrderLineClient>;
            if (unlinkedRows == null || unlinkedRows.Count == 0)
                return;

            var markedList = unlinkedRows.Where(r => r.Mark).ToList();
            if (!added)
            {
                var linkedRows = dgLinkedGrid.ItemsSource as List<SerialToOrderLineClient>;
                if (markedList.Count == 0)
                    return;
                if (linkedRows.Intersect(markedList).Count() > 0)
                {
                    UnicontaMessageBox.Show(Uniconta.ClientTools.Localization.lookup("LinkedRowErrorMsg"), Uniconta.ClientTools.Localization.lookup("Error"));
                    return;
                }
            }
            if (markedList.Count == 0)
                return;

            var _UseSerial = this.invItemMaster._UseSerial;
            List<DCOrderLineSerieBatch> olSerieBatchList = new List<DCOrderLineSerieBatch>();
            foreach (var row in markedList)
            {
                var olSerieBatch = new DCOrderLineSerieBatch();
                double qty;
                if (_UseSerial)
                    qty = dcorderlineMaster._Qty >= 0 ? 1d : -1d;
                else if (row._QtyMarked != 0)
                    qty = row._QtyMarked;
                else
                    qty = dcorderlineMaster._Qty;
                olSerieBatch._Qty = qty;
                olSerieBatch.SetMaster(row);
                olSerieBatch.SetMaster(dcorderlineMaster as UnicontaBaseEntity);
                olSerieBatchList.Add(olSerieBatch);
                if (row._Warehouse != null)
                {
                    dcorderlineMaster.Warehouse = row._Warehouse;
                    dcorderlineMaster.Location = row._Location;
                }
            }
            var err = await api.Insert(olSerieBatchList);
            if (err != ErrorCodes.Succes)
                UtilDisplay.ShowErrorCode(err);
            else
            {
                dcorderlineMaster.SerieBatchMarked = true;
                if (!saveAndExit)
                    InitQuery();
            }
        }

        SQLCache warehouse;
        private void PART_Editor_GotFocus(object sender, RoutedEventArgs e)
        {
            if (warehouse == null) return;

            var selectedItem = dgLinkedGrid.SelectedItem as SerialToOrderLineClient;
            if (selectedItem?._Warehouse != null)
            {
                var selected = (InvWarehouse)warehouse.Get(selectedItem._Warehouse);
                SetLocation(selected, selectedItem);
            }
        }

        private async void SetLocation(InvWarehouse master, SerialToOrderLineClient rec)
        {
            if (api.CompanyEntity.Location)
            {
                if (master != null)
                    rec.locationSource = master.Locations ?? await master.LoadLocations(api);
                else
                {
                    rec.locationSource = null;
                    rec.Location = null;
                }
                rec.NotifyPropertyChanged("LocationSource");
            }
        }

        protected override void OnLayoutLoaded()
        {
            base.OnLayoutLoaded();
            var company = api.CompanyEntity;
            if (!company.Location || !company.Warehouse)
            {
                Location.Visible = Location.ShowInColumnChooser = false;
                LocationCol.Visible = LocationCol.ShowInColumnChooser = false;
            }
            if (!company.Warehouse)
            {
                Warehouse.Visible = Warehouse.ShowInColumnChooser = false;
                WarehouseCol.Visible = WarehouseCol.ShowInColumnChooser = false;
            }
        }

        public override bool CheckIfBindWithUserfield(out bool isReadOnly, out bool useBinding)
        {
            isReadOnly = false;
            useBinding = true;
            return true;
        }
    }
    public class SerialToOrderLineClient : InvSerieBatchClient
    {
        bool _mark;
        public bool Mark { get { return _mark; } set { _mark = value; NotifyPropertyChanged("Mark"); } }
        public string DisplayText
        {
            get
            {
                string num = _Number;
                if (string.IsNullOrEmpty(num))
                    num = "0";
                string cp = Convert.ToString(QtyOpen);
                if (string.IsNullOrEmpty(cp))
                    cp = "0";
                return string.Format("{0}({1})", num, cp);
            }
        }
       
        internal object locationSource;
        public object LocationSource { get { return locationSource; } }
    }
}
