public static class DiscordUiText
{
    public static string Unknown(AppLanguage language) => language == AppLanguage.Korean ? "알 수 없음" : "Unknown";
    public static string Pending(AppLanguage language) => language == AppLanguage.Korean ? "대기 중" : "Pending";
    public static string None(AppLanguage language) => language == AppLanguage.Korean ? "없음" : "None";

    public static string SettlementCheckTitle(AppLanguage language) => language == AppLanguage.Korean ? "정산 확인" : "Settlement Check";
    public static string SettlementConfirmedTitle(AppLanguage language) => language == AppLanguage.Korean ? "정산 확정" : "Settlement Confirmed";
    public static string SettlementHistoryTitle(AppLanguage language) => language == AppLanguage.Korean ? "정산 기록" : "Settlement History";
    public static string SettlementHistoryDetailTitle(AppLanguage language) => language == AppLanguage.Korean ? "정산 기록 상세" : "Settlement History Detail";
    public static string SettlementField(AppLanguage language) => language == AppLanguage.Korean ? "정산" : "Settlement";
    public static string StatusField(AppLanguage language) => language == AppLanguage.Korean ? "상태" : "Status";
    public static string BuyerNameField(AppLanguage language) => language == AppLanguage.Korean ? "구매자" : "Buyer Name";
    public static string SellerNameField(AppLanguage language) => language == AppLanguage.Korean ? "판매처" : "Seller Name";
    public static string PurchaseDateField(AppLanguage language) => language == AppLanguage.Korean ? "구매일" : "Purchase Date";
    public static string ItemTotalPriceField(AppLanguage language) => language == AppLanguage.Korean ? "아이템 합계" : "Item Total Price";
    public static string TotalPriceField(AppLanguage language) => language == AppLanguage.Korean ? "총액" : "Total Price";
    public static string PayToField(AppLanguage language) => language == AppLanguage.Korean ? "송금 정보" : "Pay to";
    public static string SharedField(AppLanguage language) => language == AppLanguage.Korean ? "공동 배정" : "Shared";
    public static string IndividualField(AppLanguage language) => language == AppLanguage.Korean ? "개별 배정" : "Individual";
    public static string UnassignedField(AppLanguage language) => language == AppLanguage.Korean ? "미배정" : "Unassigned";
    public static string TaxField(AppLanguage language) => "Tax";
    public static string TipField(AppLanguage language) => "Tip";

    public static string PendingStatusText(AppLanguage language) => language == AppLanguage.Korean
        ? "영수증을 분석 중입니다. 파싱이 끝나면 같은 채널 메시지가 자동으로 갱신됩니다."
        : "The receipt is being analyzed. This message will update automatically when parsing is complete.";

    public static string ConfirmReadyFooter(AppLanguage language) => language == AppLanguage.Korean
        ? "모든 아이템이 배정되어 confirm 가능합니다."
        : "All items are assigned. Confirm is available.";

    public static string PaymentContactMissing(AppLanguage language) => language == AppLanguage.Korean
        ? "정산 수단이 입력되지 않았습니다."
        : "No payment method was provided.";

    public static string ConfirmedAtFooter(AppLanguage language, DateTimeOffset? confirmedAtUtc) => language == AppLanguage.Korean
        ? $"확정 시각 {confirmedAtUtc?.ToString("yyyy-MM-dd HH:mm")} UTC"
        : $"Confirmed at {confirmedAtUtc?.ToString("yyyy-MM-dd HH:mm")} UTC";

    public static string HistoryDetailFooter(AppLanguage language, DateTimeOffset confirmedAtUtc) => language == AppLanguage.Korean
        ? $"확정 시각 {confirmedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}"
        : $"Confirmed at {confirmedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}";

    public static string HistoryListFooter(AppLanguage language) => language == AppLanguage.Korean
        ? "상세 조회는 /history detail index:<번호> 를 사용하세요."
        : "Use /history detail index:<number> for details.";

    public static string PurchaseLabel(AppLanguage language) => language == AppLanguage.Korean ? "구매" : "purchase";
    public static string ItemsLabel(AppLanguage language) => language == AppLanguage.Korean ? "아이템" : "Items";

    public static string SelectItemButton(AppLanguage language) => language == AppLanguage.Korean ? "아이템 선택" : "Select Item";
    public static string AddItemButton(AppLanguage language) => language == AppLanguage.Korean ? "아이템 추가" : "Add Item";
    public static string RemoveItemButton(AppLanguage language) => language == AppLanguage.Korean ? "아이템 제거" : "Remove Item";
    public static string EditItemButton(AppLanguage language) => language == AppLanguage.Korean ? "아이템 수정" : "Edit Item";
    public static string MarkAlcoholButton(AppLanguage language) => language == AppLanguage.Korean ? "술 지정" : "Mark Alcohol";
    public static string TipProportionalButton(AppLanguage language) => language == AppLanguage.Korean ? "팁: 비례 배분" : "Tip: Proportional";
    public static string TipEqualButton(AppLanguage language) => language == AppLanguage.Korean ? "팁: 균등 배분" : "Tip: Equal Split";
    public static string ConfirmButton(AppLanguage language) => language == AppLanguage.Korean ? "확정" : "Confirm";
    public static string CancelButton(AppLanguage language) => language == AppLanguage.Korean ? "취소" : "Cancel";
    public static string PreviousPageButton(AppLanguage language) => language == AppLanguage.Korean ? "이전 페이지" : "Previous Page";
    public static string NextPageButton(AppLanguage language) => language == AppLanguage.Korean ? "다음 페이지" : "Next Page";

    public static string AssignPrompt(AppLanguage language, int currentPage, int totalPages) => language == AppLanguage.Korean
        ? $"아이템을 선택해서 정산에 참가하세요. (Page {currentPage}/{totalPages})"
        : $"Select items to participate in the settlement. (Page {currentPage}/{totalPages})";
    public static string RemovePrompt(AppLanguage language, int currentPage, int totalPages) => language == AppLanguage.Korean
        ? $"제거할 아이템을 선택하세요. (Page {currentPage}/{totalPages})"
        : $"Select an item to remove. (Page {currentPage}/{totalPages})";
    public static string EditPrompt(AppLanguage language, int currentPage, int totalPages) => language == AppLanguage.Korean
        ? $"수정할 아이템을 선택하세요. (Page {currentPage}/{totalPages})"
        : $"Select an item to edit. (Page {currentPage}/{totalPages})";
    public static string AlcoholPrompt(AppLanguage language, int currentPage, int totalPages) => language == AppLanguage.Korean
        ? $"alcohol 아이템을 선택하세요. (Page {currentPage}/{totalPages})"
        : $"Select alcohol items. (Page {currentPage}/{totalPages})";

    public static string AssignPlaceholder(AppLanguage language) => language == AppLanguage.Korean ? "아이템 선택" : "Select items";
    public static string RemovePlaceholder(AppLanguage language) => language == AppLanguage.Korean ? "제거할 아이템 선택" : "Select item to remove";
    public static string EditPlaceholder(AppLanguage language) => language == AppLanguage.Korean ? "수정할 아이템 선택" : "Select item to edit";
    public static string AlcoholPlaceholder(AppLanguage language) => language == AppLanguage.Korean ? "alcohol 아이템 선택" : "Select alcohol items";

    public static string SettleUpCommandDescription(AppLanguage language) => language == AppLanguage.Korean ? "정산 이미지 업로드를 시작합니다." : "Start uploading a receipt image for settlement.";
    public static string UploadReceiptButton(AppLanguage language) => language == AppLanguage.Korean ? "영수증 업로드" : "Upload Receipt";
    public static string UploadPromptText(AppLanguage language) => language == AppLanguage.Korean ? "아래 버튼을 누르면 이미지 업로드를 시작합니다." : "Press the button below to start uploading an image.";
    public static string UploadModalTitle(AppLanguage language) => language == AppLanguage.Korean ? "영수증 업로드" : "Upload Receipt";
    public static string UploadImageLabel(AppLanguage language) => language == AppLanguage.Korean ? "이미지 파일" : "Image File";
    public static string UploadImageDescription(AppLanguage language) => language == AppLanguage.Korean ? "jpg 또는 png 파일을 업로드해 주세요." : "Upload a jpg or png file.";
    public static string PaymentContactLabel(AppLanguage language) => language == AppLanguage.Korean ? "계좌번호 / 전화번호 / 이메일(zelle) - 선택, 저장되지 않음" : "Bank account / phone / email (zelle) - optional, not stored";
    public static string PaymentContactPlaceholder(AppLanguage language) => language == AppLanguage.Korean ? "예: 010-1234-5678 / example@email.com" : "Example: 555-123-4567 / example@email.com";

    public static string InvalidButtonInfo(AppLanguage language) => language == AppLanguage.Korean ? "버튼 정보가 올바르지 않습니다. `/settle-up`을 다시 실행해 주세요." : "The button information is invalid. Run `/settle-up` again.";
    public static string ButtonOwnerOnly(AppLanguage language) => language == AppLanguage.Korean ? "이 버튼은 명령어를 실행한 사용자만 사용할 수 있습니다." : "Only the user who ran the command can use this button.";
    public static string BlobNotConfigured(AppLanguage language) => language == AppLanguage.Korean ? "Blob 저장소 설정이 비어 있어 업로드할 수 없습니다. 환경변수 설정을 확인해 주세요." : "Blob storage is not configured. Check the environment variables.";
    public static string InvalidModalInfo(AppLanguage language) => language == AppLanguage.Korean ? "모달 정보가 올바르지 않습니다. `/settle-up`을 다시 실행해 주세요." : "The modal information is invalid. Run `/settle-up` again.";
    public static string ModalOwnerOnly(AppLanguage language) => language == AppLanguage.Korean ? "이 모달은 명령어를 실행한 사용자만 제출할 수 있습니다." : "Only the user who ran the command can submit this modal.";
    public static string MissingAttachment(AppLanguage language) => language == AppLanguage.Korean ? "업로드된 파일을 찾을 수 없습니다. 다시 시도해 주세요." : "No uploaded file was found. Please try again.";
    public static string UploadFailed(AppLanguage language) => language == AppLanguage.Korean ? "Blob 업로드 중 오류가 발생했습니다. 잠시 후 다시 시도해 주세요." : "An error occurred while uploading to blob storage. Please try again later.";
    public static string InvalidImageFile(AppLanguage language) => language == AppLanguage.Korean ? "jpg, jpeg, png 파일만 업로드할 수 있습니다." : "Only jpg, jpeg, and png files can be uploaded.";

    public static string TestCommandDescription(AppLanguage language) => language == AppLanguage.Korean ? "테스트 영수증 UI를 생성합니다." : "Create a test receipt UI.";
    public static string TestScenarioDescription(AppLanguage language) => language == AppLanguage.Korean ? "테스트할 영수증 시나리오를 선택합니다." : "Select a receipt scenario to test.";
    public static string TestSessionError(AppLanguage language) => language == AppLanguage.Korean ? "테스트 영수증 세션 생성 중 오류가 발생했습니다. 로그를 확인해 주세요." : "An error occurred while creating the test receipt session. Check the logs.";

    public static string PingCommandDescription(AppLanguage language) => language == AppLanguage.Korean ? "봇 응답을 테스트합니다." : "Test bot responsiveness.";
    public static string PingResponse(AppLanguage language) => language == AppLanguage.Korean ? "pong! slash command 정상 작동 중입니다." : "pong! The slash command is working normally.";

    public static string HistoryCommandDescription(AppLanguage language) => language == AppLanguage.Korean ? "최근 정산 기록을 조회합니다." : "View recent settlement history.";
    public static string HistoryListDescription(AppLanguage language) => language == AppLanguage.Korean ? "최근 정산 기록 목록을 조회합니다." : "View a list of recent settlement history.";
    public static string HistoryDetailDescription(AppLanguage language) => language == AppLanguage.Korean ? "현재 시점 기준 최신순 n번째 기록을 상세 조회합니다." : "View the details for the nth most recent history entry.";
    public static string HistoryIndexDescription(AppLanguage language) => language == AppLanguage.Korean ? "현재 시점 기준 최신순 n번째 기록" : "The nth most recent history entry.";
    public static string HistoryStorageNotConfigured(AppLanguage language) => language == AppLanguage.Korean ? "history 저장소가 설정되지 않았습니다." : "History storage is not configured.";
    public static string HistoryUsage(AppLanguage language) => language == AppLanguage.Korean ? "사용 방법: `/history list` 또는 `/history detail index:<번호>`" : "Usage: `/history list` or `/history detail index:<number>`";
    public static string HistoryIndexRequired(AppLanguage language) => language == AppLanguage.Korean ? "index 값이 필요합니다." : "The index value is required.";
    public static string HistoryIndexRange(AppLanguage language) => language == AppLanguage.Korean ? "index는 1부터 10 사이여야 합니다." : "The index must be between 1 and 10.";
    public static string HistoryNotFound(AppLanguage language, long index) => language == AppLanguage.Korean ? $"현재 {index}번째 기록을 찾을 수 없습니다." : $"Could not find the current #{index} history entry.";
    public static string HistoryEmpty(AppLanguage language) => language == AppLanguage.Korean ? "저장된 정산 기록이 없습니다." : "There is no saved settlement history.";

    public static string LanguageCommandDescription(AppLanguage language) => language == AppLanguage.Korean ? "사용 언어를 설정합니다." : "Set your UI language.";
    public static string LanguageOptionDescription(AppLanguage language) => language == AppLanguage.Korean ? "사용할 언어를 선택합니다." : "Choose the language to use.";
    public static string LanguageUpdated(AppLanguage language, AppLanguage selectedLanguage) => language == AppLanguage.Korean
        ? $"언어를 {(selectedLanguage == AppLanguage.Korean ? "한국어" : "영어")}로 설정했습니다."
        : $"Language set to {(selectedLanguage == AppLanguage.Korean ? "Korean" : "English")}.";

    public static string SessionNotFound(AppLanguage language) => language == AppLanguage.Korean ? "해당 영수증 세션을 찾을 수 없습니다." : "Could not find that receipt session.";
    public static string CustomCommandDescription(AppLanguage language) => language == AppLanguage.Korean ? "직접 정산용 빈 영수증을 만듭니다." : "Create a blank receipt for manual settlement.";
    public static string CustomPaymentContactDescription(AppLanguage language) => language == AppLanguage.Korean ? "송금 정보 - 선택" : "Payment contact - optional";
    public static string CustomCommandError(AppLanguage language) => language == AppLanguage.Korean ? "커스텀 영수증 세션 생성 중 오류가 발생했습니다. 로그를 확인해 주세요." : "An error occurred while creating the custom receipt session. Check the logs.";
    public static string DraftNotReady(AppLanguage language) => language == AppLanguage.Korean ? "영수증 분석이 아직 끝나지 않았습니다." : "Receipt analysis is not finished yet.";
    public static string OwnerOnlyFeature(AppLanguage language) => language == AppLanguage.Korean ? "정산자만 이 기능을 사용할 수 있습니다." : "Only the owner can use this feature.";
    public static string OwnerOnlyRemove(AppLanguage language) => language == AppLanguage.Korean ? "정산자만 아이템을 제거할 수 있습니다." : "Only the owner can remove items.";
    public static string RemoveItemNotFound(AppLanguage language) => language == AppLanguage.Korean ? "제거할 아이템을 찾을 수 없습니다." : "Could not find an item to remove.";
    public static string OwnerOnlyEdit(AppLanguage language) => language == AppLanguage.Korean ? "정산자만 아이템을 수정할 수 있습니다." : "Only the owner can edit items.";
    public static string EditItemNotFound(AppLanguage language) => language == AppLanguage.Korean ? "수정할 아이템을 찾을 수 없습니다." : "Could not find an item to edit.";
    public static string EditItemModalTitle(AppLanguage language) => language == AppLanguage.Korean ? "아이템 수정" : "Edit Item";
    public static string AddItemModalTitle(AppLanguage language) => language == AppLanguage.Korean ? "아이템 추가" : "Add Item";
    public static string ItemNameLabel(AppLanguage language) => language == AppLanguage.Korean ? "아이템 이름" : "Item Name";
    public static string ItemPriceLabel(AppLanguage language) => language == AppLanguage.Korean ? "아이템 가격" : "Item Price";
    public static string ItemPricePlaceholder(AppLanguage language) => language == AppLanguage.Korean ? "예: 12.50" : "Example: 12.50";
    public static string ItemQuantityLabel(AppLanguage language) => language == AppLanguage.Korean ? "수량" : "Quantity";
    public static string ItemQuantityPlaceholder(AppLanguage language) => language == AppLanguage.Korean ? "기본값 1" : "Default: 1";
    public static string OwnerOnlyAlcohol(AppLanguage language) => language == AppLanguage.Korean ? "정산자만 alcohol 아이템을 지정할 수 있습니다." : "Only the owner can mark alcohol items.";
    public static string OwnerOnlyAdd(AppLanguage language) => language == AppLanguage.Korean ? "정산자만 아이템을 추가할 수 있습니다." : "Only the owner can add items.";
    public static string InvalidItemName(AppLanguage language) => language == AppLanguage.Korean ? "아이템 이름을 입력해 주세요." : "Enter an item name.";
    public static string InvalidItemPrice(AppLanguage language) => language == AppLanguage.Korean ? "아이템 가격은 0 이상의 숫자로 입력해 주세요." : "Item price must be a number greater than or equal to 0.";
    public static string InvalidItemQuantity(AppLanguage language) => language == AppLanguage.Korean ? "수량은 1 이상의 정수로 입력해 주세요." : "Quantity must be an integer greater than or equal to 1.";
    public static string OwnerOnlyConfirm(AppLanguage language) => language == AppLanguage.Korean ? "confirm은 정산자만 누를 수 있습니다." : "Only the owner can press confirm.";
    public static string HistorySaveFailed(AppLanguage language) => language == AppLanguage.Korean ? "history등록에 실패했습니다." : "Failed to save history.";
    public static string OwnerOnlyCancel(AppLanguage language) => language == AppLanguage.Korean ? "세션 종료는 정산자만 할 수 있습니다." : "Only the owner can cancel the session.";
    public static string OwnerOnlyTipMode(AppLanguage language) => language == AppLanguage.Korean ? "정산자만 tip 분배 방식을 바꿀 수 있습니다." : "Only the owner can change the tip split mode.";
    public static string TipNotAvailable(AppLanguage language) => language == AppLanguage.Korean ? "이 영수증에는 tip이 없습니다." : "This receipt does not include a tip.";
    public static string EditItemTokenMissing(AppLanguage language) => language == AppLanguage.Korean ? "수정 대상 아이템 정보를 찾을 수 없습니다. 다시 시도해 주세요." : "Could not find edit target information. Please try again.";
    public static string ItemEdited(AppLanguage language) => language == AppLanguage.Korean ? "아이템을 수정했습니다." : "The item was updated.";

    public static string ConfirmBlockDraftNotReady(AppLanguage language) => language == AppLanguage.Korean ? "영수증 분석이 아직 끝나지 않았습니다." : "Receipt analysis is not finished yet.";
    public static string ConfirmBlockAlreadyConfirmed(AppLanguage language) => language == AppLanguage.Korean ? "이미 confirm된 영수증입니다." : "This receipt has already been confirmed.";
    public static string ConfirmBlockNoItems(AppLanguage language) => language == AppLanguage.Korean ? "아이템이 없어 confirm할 수 없습니다." : "You cannot confirm a receipt with no items.";
    public static string ConfirmBlockUnassigned(AppLanguage language) => language == AppLanguage.Korean ? "Unassigned 아이템이 모두 배정되어야 confirm할 수 있습니다." : "All unassigned items must be assigned before confirming.";
    public static string ConfirmBlockAlcohol(AppLanguage language) => language == AppLanguage.Korean ? "SST/SLT가 있는 영수증은 alcohol 아이템을 먼저 지정해야 합니다." : "Receipts with SST/SLT require alcohol items to be marked before confirming.";
}
