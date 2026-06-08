from datetime import datetime
from sqlmodel import Field, SQLModel

class TabliceUzytkownicy(SQLModel, table=True):
    __tablename__ = "tablice_uzytkownicy"
    
    id_uzytkownika: int = Field(foreign_key="uzytkownicy.id_uzytkownika", primary_key=True)
    id_tablicy: int = Field(foreign_key="tablice.id_tablicy", primary_key=True)
    rola: str = Field(default="member", description="admin, member, viewer")
    data_dolaczenia: datetime = Field(default_factory=datetime.now(timezone.utc))