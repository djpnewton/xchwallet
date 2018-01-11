from sqlalchemy import create_engine
from sqlalchemy.orm import scoped_session, sessionmaker
from sqlalchemy.ext.declarative import declarative_base
import os
from config import Cfg

cfg = Cfg()
dir_path = os.path.dirname(os.path.realpath(__file__))
if cfg.testnet:
    engine = create_engine("sqlite:///%s/gethtxscan_testnet.db" % dir_path, convert_unicode=True)
else:
    engine = create_engine("sqlite:///%s/gethtxscan.db" % dir_path, convert_unicode=True)
db_session = scoped_session(sessionmaker(autocommit=False,
                                         autoflush=False,
                                         bind=engine))
Base = declarative_base()
Base.query = db_session.query_property()

def init_db():
    # import all modules here that might define models so that
    # they will be registered properly on the metadata.  Otherwise
    # you will have to import them first before calling init_db()
    import models
    Base.metadata.create_all(bind=engine)
